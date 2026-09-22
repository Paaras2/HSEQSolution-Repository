using HSEQ.Shared.Interfaces.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace HSEQ.Shared.Services.Services
{
    /// <summary>
    /// خواندنِ مستقیم نام کاربران از دیتابیس سامانه‌ی مدیریت کاربران - همان دیتابیسی
    /// که UmDatabaseService برای احراز هویت می‌خواند (dbo.Users)، اما این سرویس از
    /// سوییچِ UserManagement:Source مستقل است: چه ورودِ کاربران از راهِ سرویس HTTP
    /// سنجیده شود چه مستقیم از دیتابیس، این دیتابیس روی همان SQL Server و با همان
    /// اعتبارنامه در دسترس است (به مستندِ UmConnectionString مراجعه کنید).
    ///
    /// نام فقط برای نمایش است، نه تصمیم‌گیری - برای همین این سرویس هرگز پرتاب
    /// نمی‌کند. اگر دیتابیس UM در دسترس نبود، فراخوان یک دیکشنری خالی می‌گیرد و
    /// کلاینت به‌جای نام، کد پرسنلی را نشان می‌دهد؛ نبودِ نام نباید صفحه‌ی سند را
    /// بخواباند.
    /// </summary>
    public class UmDirectoryService : IUmDirectoryService
    {
        private readonly string _connectionString;
        private readonly ILogger<UmDirectoryService> _logger;
        private readonly string _database;
        private readonly string _server;

        public UmDirectoryService(string connectionString, ILogger<UmDirectoryService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;

            // نام سرور و دیتابیس فقط برای پیام لاگ - همان الگوی UmDatabaseService،
            // تا رشته‌ی اتصال (با رمز داخلش) جایی چاپ نشود.
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

        // سقفِ کدها در هر رفت‌وبرگشت. نمای درختی می‌تواند تا هزار سند یک‌جا بگیرد و
        // SQL Server هم زیر ۲۱۰۰ پارامتر در هر کوئری سقف دارد؛ دسته‌بندی، بدون
        // نزدیک شدن به آن سقف، همان یک رفت‌وبرگشتی را که بقیه‌ی این سرویس فرض می‌کند
        // برای حالتِ معمول (چند ده کد پرسنلی) حفظ می‌کند.
        private const int BatchSize = 1000;

        public async Task<Dictionary<int, string>> GetFullNamesAsync(IEnumerable<int> pCodes)
        {
            var codes = pCodes.Where(p => p > 0).Distinct().ToList();
            var result = new Dictionary<int, string>();
            if (codes.Count == 0)
                return result;

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                for (var offset = 0; offset < codes.Count; offset += BatchSize)
                {
                    var batch = codes.Skip(offset).Take(BatchSize).ToList();

                    // پارامترهای جداگانه به‌جای رشته‌سازی مستقیمِ کدها در SQL.
                    var parameterNames = batch.Select((_, i) => "@p" + i).ToList();
                    var sql =
                        "SELECT PCode, FirstName, LastName FROM dbo.Users WHERE PCode IN (" +
                        string.Join(",", parameterNames) + ")";

                    await using var command = new SqlCommand(sql, connection);
                    for (var i = 0; i < batch.Count; i++)
                        command.Parameters.Add(parameterNames[i], SqlDbType.Int).Value = batch[i];

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var pCode = reader.GetInt32(reader.GetOrdinal("PCode"));
                        var firstName = reader["FirstName"] as string ?? "";
                        var lastName = reader["LastName"] as string ?? "";
                        var fullName = $"{firstName} {lastName}".Trim();
                        if (fullName.Length > 0)
                            result[pCode] = fullName;
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "نام‌های کاربری خوانده نشد - {Reason}", UmSqlFailure.Describe(ex, _database, _server));
            }

            return result;
        }
    }
}
