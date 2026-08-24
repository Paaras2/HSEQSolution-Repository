using Microsoft.Extensions.Configuration;
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
                if (string.IsNullOrWhiteSpace(configuration[key]))
                    errors.Add($"تنظیم اجباری '{key}' مقدار ندارد.");
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

                // برخلاف دیتابیس، سرویس مدیریت کاربران همیشه روی میزبان دیگری است؛
                // نشانی محلی اینجا یعنی تنظیمات جابه‌جا شده.
                var umUrl = configuration["UserManagementAPI:Url"] ?? string.Empty;
                if (umUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                    umUrl.Contains("127.0.0.1", StringComparison.Ordinal))
                {
                    errors.Add("نشانی سرویس مدیریت کاربران به میزبان محلی اشاره می‌کند.");
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
    }
}
