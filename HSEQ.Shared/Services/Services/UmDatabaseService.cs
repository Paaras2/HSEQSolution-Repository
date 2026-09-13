using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Shared.Interfaces.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading.Tasks;

namespace HSEQ.Shared.Services.Services
{
    /// <summary>
    /// اعتبارسنجی کاربر با خواندن مستقیم از دیتابیس سامانه‌ی مدیریت کاربران،
    /// به‌جای فراخوانی HTTP سرویس آن.
    ///
    /// چرا: سرویس UM از سرور برنامه در دسترس نبود - اول بسته‌ها به ۸۰۳۰ دور ریخته
    /// می‌شدند و بعد نشانیِ نامی به‌جای JSON صفحه‌ی HTML برمی‌گرداند. دیتابیس UM
    /// روی همان SQL Server دیتابیس اصلی است و با همان اعتبارنامه در دسترس.
    ///
    /// هزینه‌اش را باید دانست: منطق راستی‌آزمایی رمز حالا در دو جا وجود دارد. اگر
    /// سامانه‌ی UM قاعده‌ای اضافه کند - قفل حساب، انقضای رمز، اجبار به تغییر - این
    /// مسیر از آن بی‌خبر می‌ماند. جدول‌های RbacOtpCode و RbacUserTotp نشان می‌دهند
    /// چنین قواعدی از قبل آنجا هستند.
    ///
    /// برای همین این مسیر با تنظیمات فعال می‌شود و پیش‌فرض نیست:
    ///     "UserManagement": { "Source": "Database" }
    /// </summary>
    public class UmDatabaseService : IUMService
    {
        private readonly string _connectionString;
        private readonly UmPasswordVerifier _passwordVerifier;
        private readonly ILogger<UmDatabaseService> _logger;
        private readonly string _database;
        private readonly string _server;

        // ورودیِ فرم «کد پرسنلی» است و کلاینت آن را در فیلد Username می‌فرستد -
        // همان قراردادی که سرویس HTTP هم داشت. پس تطبیق روی ستون Username است.
        private const string LookupSql = @"
SELECT TOP 2 PCode, FirstName, LastName, Mobile, IsActive, IsFirstLogin,
             NationalCode, Username, LastModificationDate, Password
FROM dbo.Users
WHERE Username = @username";

        public UmDatabaseService(
            string connectionString,
            UmPasswordVerifier passwordVerifier,
            ILogger<UmDatabaseService> logger)
        {
            _connectionString = connectionString;
            _passwordVerifier = passwordVerifier;
            _logger = logger;

            // نام سرور و دیتابیس برای پیام خطا جدا نگه داشته می‌شود تا هنگام خرابی
            // لازم نباشد رشته‌ی اتصال - که رمز داخلش است - جایی باز شود.
            try
            {
                var parsed = new SqlConnectionStringBuilder(connectionString);
                _database = parsed.InitialCatalog;
                _server = parsed.DataSource;
            }
            catch (ArgumentException)
            {
                _database = "?";
                _server = "?";
            }
        }

        public async Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrEmpty(request.Password))
                throw new ExternalAuthException(InvalidCredentials, 401);

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new SqlCommand(LookupSql, connection);
                command.Parameters.Add("@username", SqlDbType.NVarChar, 256).Value = request.Username.Trim();

                await using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    // کاربر پیدا نشد. پیام همان پیام رمز غلط است تا از بیرون نشود
                    // فهمید کدام کد پرسنلی وجود دارد.
                    _logger.LogInformation("ورود ناموفق: کاربری با این نام کاربری در دیتابیس UM نیست.");
                    throw new ExternalAuthException(InvalidCredentials, 401);
                }

                var stored = reader["Password"] as string;
                var credential = ReadCredential(reader);

                // بیش از یک ردیف یعنی نام کاربری یکتا نیست و نمی‌شود مطمئن بود کدام
                // کاربر منظور است. پذیرفتنِ اولی یعنی ورود به حساب اشتباه.
                if (await reader.ReadAsync())
                {
                    _logger.LogError(
                        "بیش از یک کاربر با همین نام کاربری در دیتابیس UM هست؛ ورود رد شد تا به حساب اشتباه وارد نشود.");
                    throw new ExternalAuthException(InvalidCredentials, 401);
                }

                var result = _passwordVerifier.Verify(stored, request.Password);

                if (result == PasswordVerificationResult.UnsupportedFormat)
                {
                    // ایرادِ ماست، نه کاربر. باید در لاگ دیده شود، ولی به مرورگر
                    // چیزی از آن نمی‌رود.
                    //
                    // سه قالبِ شناخته‌شده پوشش داده شده‌اند؛ رسیدن به اینجا یعنی
                    // سامانه‌ی UM قالب چهارمی نوشته است. طولِ مقدار تنها چیزی است که
                    // لاگ می‌شود - خودِ مقدار ماده‌ی اعتبارنامه است.
                    _logger.LogError(
                        "رمز این کاربر با قالبی ذخیره شده که این مسیر نمی‌شناسد (طول {Length}). " +
                        "برای این کاربر باید موقتاً به حالت «UserManagement:Source = Api» برگشت.",
                        stored?.Length ?? 0);
                    throw new ExternalAuthException(Unavailable, 503);
                }

                if (result == PasswordVerificationResult.Failed)
                    throw new ExternalAuthException(InvalidCredentials, 401);

                return credential;
            }
            catch (ExternalAuthException)
            {
                throw;
            }
            catch (SqlException ex)
            {
                // خرابیِ دیتابیس UM نباید خطای ۵۰۰ بدهد - همان رفتار مسیر HTTP.
                // پیام دقیقاً می‌گوید کدام خرابی است، وگرنه «اتصال ممکن نشد» برای
                // چهار مشکلِ کاملاً متفاوت یکسان نوشته می‌شد.
                _logger.LogError(ex, "{Reason}", UmSqlFailure.Describe(ex, _database, _server));
                throw new ExternalAuthException(Unavailable, 503);
            }
        }

        private static CheckCredentialDto ReadCredential(SqlDataReader reader) => new()
        {
            PCode = reader["PCode"] as int? ?? 0,
            FirstName = reader["FirstName"] as string,
            LastName = reader["LastName"] as string,
            Mobile = reader["Mobile"] as string,
            IsActive = reader["IsActive"] as bool? ?? false,
            IsFirstLogin = reader["IsFirstLogin"] as bool? ?? false,
            NationalCode = reader["NationalCode"] as string,
            UserName = reader["Username"] as string,
            LastModificationDate = reader["LastModificationDate"] as DateTime?,
        };

        private const string InvalidCredentials = "نام کاربری یا رمز عبور نامعتبر است";
        private const string Unavailable = "ارتباط با سرویس احراز هویت برقرار نشد. لطفاً چند لحظه بعد دوباره تلاش کنید.";
    }
}
