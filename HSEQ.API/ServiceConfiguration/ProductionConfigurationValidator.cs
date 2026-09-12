using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;

namespace HSEQ.API.ServiceConfiguration
{
    // اعتبارسنجی تنظیمات پیش از بالا آمدن برنامه.
    //
    // بدون این، یک کلید جاافتاده در محیط عملیاتی به‌صورت NullReferenceException وسط
    // اولین درخواست کاربر ظاهر می‌شد - یا بدتر، برنامه با مقدارِ توسعه بالا می‌آمد و
    // کسی متوجه نمی‌شد. اینجا برنامه با پیام صریح بالا نمی‌آید.
    public static class ProductionConfigurationValidator
    {
        // هر کلیدی که داخل مخزن نوشته شده باشد، عمومی است: هر کسی که به کد دسترسی دارد
        // می‌تواند با آن توکن جعلی بسازد. به‌جای فهرست کردن یک مقدار مشخص، هر کلیدی که
        // این نشانه را داشته باشد در محیط عملیاتی رد می‌شود - این‌طور با هر بار عوض شدن
        // کلیدِ توسعه، اعتبارسنجی هم به‌روزرسانی لازم ندارد.
        private const string DevelopmentKeyMarker = "DEVELOPMENT-ONLY";

        // کلید قبلیِ مخزن که در تاریخچه‌ی گیت و روی GitHub منتشر شده است. از فایل برداشته
        // شد، ولی نصب‌های قدیمی ممکن است هنوز رونوشتی از آن داشته باشند، پس نامش صریحاً
        // در فهرست ممنوع می‌ماند.
        private const string LeakedDevelopmentJwtKey =
            "EAAAAEtB4PDHGBXZv5pePrGLvzB5FVb+1UTuUPNa37QyDA2jpHEi0p1K9ZOzPSYgoc5ZQDZgYgPk5eswRrj1UVI1DPg=";

        // HMAC-SHA256 حداقل ۳۲ بایت کلید می‌خواهد.
        private const int MinimumJwtKeyLength = 32;

        private static readonly string[] RequiredKeys =
        {
            "ConnectionStrings:DefaultConnection",
            "Jwt:Key",
            "Jwt:Issuer",
            "Jwt:Audience",
            "Jwt:ExpiryInMinutes",
            "UploadPath",
            "UserManagementAPI:Url",
        };

        public static void Validate(IConfiguration configuration, IHostEnvironment environment)
        {
            var errors = new List<string>();

            foreach (var key in RequiredKeys)
            {
                var value = configuration[key];

                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"تنظیم اجباری '{key}' مقدار ندارد.");
                    continue;
                }

                // قالبِ تنظیمات با جای‌نگهدارهایی مثل <SQL-SERVER-HOST> می‌آید و کسی
                // باید پرشان کند. اگر پر نشوند، هیچ‌کدام از بررسی‌های دیگر جلویشان را
                // نمی‌گرفت و نتیجه دو خرابیِ بد بود:
                //
                //   - Jwt:Key برابر «<NEW-BASE64-SIGNING-KEY-AT-LEAST-32-CHARS>» از هر
                //     بررسی‌ای رد می‌شد (بلندتر از ۳۲ کاراکتر است و نشانه‌ی توسعه هم
                //     ندارد). یعنی برنامه بالا می‌آمد و توکن‌ها را با رشته‌ای امضا می‌کرد
                //     که داخل همین مخزن نوشته شده - هر کسی می‌توانست توکن جعلی بسازد.
                //
                //   - رشته‌ی اتصال با میزبانِ <SQL-SERVER-HOST> باعث می‌شد راه‌اندازی
                //     دقیقه‌ها روی تلاش‌های ناموفق اتصال بماند، بدون هیچ پیامی که بگوید
                //     علتش یک فایل تنظیماتِ پرنشده است.
                //
                // پس جای‌نگهدارِ باقی‌مانده همان اول و با پیام صریح رد می‌شود.
                if (ContainsPlaceholder(value))
                    errors.Add($"تنظیم '{key}' هنوز جای‌نگهدارِ قالب را دارد و پر نشده است.");
            }

            if (!int.TryParse(configuration["Jwt:ExpiryInMinutes"], out var expiry) || expiry <= 0)
                errors.Add("تنظیم 'Jwt:ExpiryInMinutes' باید یک عدد صحیح مثبت باشد.");

            // بررسی‌های زیر فقط بیرون از محیط توسعه معنا دارند: کار روزمره‌ی توسعه‌دهنده
            // نباید به داشتن کلید اختصاصی و دامنه‌ی واقعی گره بخورد.
            if (!environment.IsDevelopment())
            {
                var jwtKey = configuration["Jwt:Key"];

                if (jwtKey == LeakedDevelopmentJwtKey ||
                    (jwtKey?.Contains(DevelopmentKeyMarker, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    errors.Add(
                        "کلید JWT یک کلیدِ توسعه است و داخل مخزن نوشته شده. برای محیط عملیاتی " +
                        "کلید تازه بسازید و از راه appsettings.Production.json یا متغیر محیطی " +
                        "Jwt__Key بدهید.");
                }

                if (!string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Length < MinimumJwtKeyLength)
                    errors.Add($"کلید JWT باید حداقل {MinimumJwtKeyLength} کاراکتر باشد.");

                // LocalDB یک موتور فقط-توسعه است و روی سرور اصلاً وجود ندارد؛ دیدنش
                // یعنی تنظیمات ماشین توسعه‌دهنده منتشر شده. اما خودِ localhost در رشته‌ی
                // اتصال ایراد نیست: نصب SQL Server روی همان سرور IIS چیدمان کاملاً
                // متعارفی است، پس عمداً رد نمی‌شود.
                var connection = configuration["ConnectionStrings:DefaultConnection"] ?? string.Empty;
                if (connection.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
                    errors.Add("رشته‌ی اتصال هنوز به LocalDB اشاره می‌کند که موتوری فقط برای توسعه است.");

                // نشانی محلیِ سرویس مدیریت کاربران.
                //
                // چیزی که این بررسی واقعاً از آن محافظت می‌کند، Start-UmSimulator.ps1
                // است: شبیه‌سازی که *هر رمزی* را می‌پذیرد و روی loopback گوش می‌دهد.
                // اشاره‌ی تنظیمات عملیاتی به آن، یعنی احراز هویت سامانه عملاً دور زده
                // شده - بی‌آنکه چیزی خراب به نظر برسد.
                //
                // ولی «همیشه روی میزبان دیگری است» فرض غلطی بود: نصب UM روی همان سرور
                // IIS کاملاً متعارف است، و در آن حالت loopback حتی بهتر است، چون
                // ترافیک از ماشین بیرون نمی‌رود و به قواعد فایروال هم گره نمی‌خورد.
                //
                // پس رد کردن می‌ماند، اما با یک درِ صریح: باید آگاهانه بازش کنید.
                // تفاوتش با نبودِ بررسی این است که «اتفاقی» نمی‌شود - کسی باید عمداً
                // این کلید را بنویسد، و نوشتنش در فایل تنظیمات ثبت می‌ماند.
                var umUrl = configuration["UserManagementAPI:Url"] ?? string.Empty;

                // شکل خودِ نشانی، پیش از هر چیز دیگر.
                //
                // یک اسلشِ جاافتاده - «http:/host» به‌جای «http://host» - یک نشانیِ
                // نامعتبر می‌سازد. آن خطا در زمان *اولین تلاش ورود* پرتاب می‌شود، نه
                // هنگام راه‌اندازی، و چون UriFormatException است نه HttpRequestException،
                // از دستِ مهارِ «سرویس در دسترس نیست» هم رد می‌شود و به‌صورت خطای ۵۰۰
                // به کاربر می‌رسد. یعنی یک کاراکتر، ورود همه را می‌شکند و پیامش هم هیچ
                // اشاره‌ای به علت ندارد.
                //
                // اینجا همان اشتباه، هنگام بالا آمدن و با پیام صریح گرفته می‌شود.
                if (!string.IsNullOrWhiteSpace(umUrl) &&
                    (!Uri.TryCreate(umUrl, UriKind.Absolute, out var parsedUmUrl) ||
                     (parsedUmUrl.Scheme != Uri.UriSchemeHttp && parsedUmUrl.Scheme != Uri.UriSchemeHttps)))
                {
                    errors.Add(
                        "نشانی سرویس مدیریت کاربران یک URL معتبر http/https نیست. " +
                        "شکل درست: http://<میزبان>:<پورت>/api/ - دو اسلش پس از ':' و یک اسلش در انتها.");
                }

                var umIsLoopback = umUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                                   umUrl.Contains("127.0.0.1", StringComparison.Ordinal);

                if (umIsLoopback && !configuration.GetValue("UserManagementAPI:AllowLoopback", false))
                {
                    errors.Add(
                        "نشانی سرویس مدیریت کاربران به میزبان محلی اشاره می‌کند. اگر سرویس UM " +
                        "واقعاً روی همین سرور است، 'UserManagementAPI:AllowLoopback' را true کنید. " +
                        "هرگز این را برای اشاره به شبیه‌ساز UM استفاده نکنید: آن هر رمزی را می‌پذیرد.");
                }

                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (origins.Length == 0)
                    errors.Add("هیچ مقداری برای 'Cors:AllowedOrigins' تنظیم نشده؛ در این حالت همه‌ی دامنه‌ها مجاز می‌شوند.");
            }

            if (errors.Count > 0)
            {
                // مقادیر عمداً چاپ نمی‌شوند - فقط نام کلید - تا رشته‌ی اتصال یا کلید
                // امضا داخل لاگ راه‌اندازی ننشیند.
                throw new InvalidOperationException(
                    $"تنظیمات محیط '{environment.EnvironmentName}' معتبر نیست:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
            }
        }

        // شکلِ جای‌نگهدار در قالب: <SQL-SERVER-HOST> ، <NEW-BASE64-SIGNING-KEY...> و مانند
        // آن‌ها - حروف بزرگ، رقم، خط تیره و زیرخط داخل < >. عمداً تنگ گرفته شده تا با
        // مقدارِ واقعی‌ای که تصادفاً < دارد اشتباه نشود.
        private static bool ContainsPlaceholder(string value) =>
            Regex.IsMatch(value, "<[A-Z0-9][A-Z0-9_-]*>");
    }
}
