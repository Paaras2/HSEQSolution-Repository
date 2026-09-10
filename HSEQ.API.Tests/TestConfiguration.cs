using System.Runtime.CompilerServices;

namespace HSEQ.API.Tests;

/// <summary>
/// تنظیمات آزمون، پیش از ساخته شدن هر میزبان.
///
/// چرا متغیر محیطی و نه ‎ConfigureAppConfiguration‎: بخشی از ‎Program.cs‎ - اعتبارسنجی
/// تنظیمات و ‎AppSettingFactory.Initialize‎ - *پیش از* ‎builder.Build()‎ اجرا می‌شود، و
/// منابعی که ‎ConfigureAppConfiguration‎ اضافه می‌کند تازه هنگام Build اعمال می‌شوند.
/// یعنی آن مقادیر هرگز به آن کد نمی‌رسیدند: آزمون‌ها بی‌سروصدا با ‎appsettings‎ ماشین
/// توسعه‌دهنده اجرا می‌شدند و به دیتابیس محلی وصل می‌شدند - از جمله ‎MigrateAsync‎ و
/// seed هنگام راه‌اندازی.
///
/// متغیر محیطی از همان اولین لحظه در ‎builder.Configuration‎ هست (‎AddEnvironmentVariables‎
/// جزو منابع پیش‌فرض است). ضمناً این دقیقاً همان سازوکاری است که برای محیط عملیاتی
/// مستند شده - ‎ConnectionStrings__DefaultConnection‎ و بقیه - پس خودِ آن سازوکار هم
/// اینجا آزموده می‌شود.
/// </summary>
internal static class TestConfiguration
{
    /// <summary>
    /// نشانی‌ای که هیچ‌وقت جواب نمی‌دهد و سریع رد می‌شود. هیچ آزمونی نباید به دیتابیس
    /// واقعی برسد؛ اگر رسید یعنی این تنظیمات اعمال نشده و آزمون بی‌معنا شده است.
    /// </summary>
    public const string UnreachableDatabase =
        "Server=127.0.0.1,1;Database=HSEQDb_NoTestEverTouchesThis;User Id=none;Password=none;" +
        "TrustServerCertificate=True;Encrypt=False;Connect Timeout=1;MultipleActiveResultSets=true";

    [ModuleInitializer]
    internal static void Apply()
    {
        Set("ConnectionStrings__DefaultConnection", UnreachableDatabase);
        Set("Jwt__Key", "hseq-test-signing-key-with-at-least-32-characters");
        Set("Jwt__Issuer", "OdccPM");
        Set("Jwt__Audience", "Audience");
        Set("Jwt__ExpiryInMinutes", "60");
        Set("UploadPath", Path.Combine(Path.GetTempPath(), "hseq-tests"));
        Set("UserManagementAPI__Url", "http://um.invalid:8030/api/");
        Set("Cors__AllowedOrigins__0", "https://hseq.odcc.local");

        // صریح و در هر دو محیط: بدون این، مقدار پیش‌فرضِ MigrateOnStartup در محیط
        // توسعه true است و اجرای آزمون‌ها ساختار دیتابیس را عوض می‌کرد.
        Set("Database__MigrateOnStartup", "false");
        Set("Database__SeedOnStartup", "false");
    }

    private static void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);
}
