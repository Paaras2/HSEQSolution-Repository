using HSEQ.API.ServiceConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// اعتبارسنجی تنظیمات، پیش از بالا آمدن برنامه.
///
/// این آزمون‌ها پس از یک آزمایش واقعی روی خروجی نهایی نوشته شدند: بسته‌ی تحویلی با
/// همان appsettings.Production.json پرنشده اجرا شد تا ببینیم چه می‌شود.
/// </summary>
public class ConfigurationValidationTests
{
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HSEQ.API";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    /// <summary>مقادیر معتبر و کامل - همان چیزی که روی سرور باید باشد.</summary>
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["ConnectionStrings:DefaultConnection"] =
            "Server=sql01;Database=HSEQDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
        ["Jwt:Key"] = "a-real-signing-key-of-at-least-32-characters",
        ["Jwt:Issuer"] = "OdccPM",
        ["Jwt:Audience"] = "Audience",
        ["Jwt:ExpiryInMinutes"] = "60",
        ["UploadPath"] = @"C:\ApplicationData\HSEQ\Documents\",
        ["UserManagementAPI:Url"] = "http://172.17.0.254:8030/api/",
        ["Cors:AllowedOrigins:0"] = "https://hseq.odcc.local",
    };

    private static InvalidOperationException? Validate(Dictionary<string, string?> settings, string environmentName)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var environment = new FakeEnvironment { EnvironmentName = environmentName };

        return Record.Exception(() => ProductionConfigurationValidator.Validate(configuration, environment))
            as InvalidOperationException;
    }

    [Fact]
    public void A_fully_filled_production_configuration_is_accepted()
    {
        Assert.Null(Validate(ValidSettings(), Environments.Production));
    }

    /// <summary>
    /// مهم‌ترین مورد. جای‌نگهدارِ کلید امضا از هر بررسی دیگری رد می‌شد: بلندتر از ۳۲
    /// کاراکتر است و نشانه‌ی «کلید توسعه» هم ندارد. یعنی برنامه بالا می‌آمد و توکن‌ها
    /// را با رشته‌ای امضا می‌کرد که متنش داخل همین مخزن نوشته شده - هر کسی می‌توانست
    /// توکن جعلیِ معتبر بسازد.
    /// </summary>
    [Fact]
    public void An_unfilled_signing_key_placeholder_is_rejected()
    {
        var settings = ValidSettings();
        settings["Jwt:Key"] = "<NEW-BASE64-SIGNING-KEY-AT-LEAST-32-CHARS>";

        var error = Validate(settings, Environments.Production);

        Assert.NotNull(error);
        Assert.Contains("Jwt:Key", error!.Message);
    }

    /// <summary>
    /// جای‌نگهدارِ میزبان دیتابیس باعث می‌شد راه‌اندازی دقیقه‌ها روی تلاش‌های ناموفق
    /// اتصال بماند و برنامه اصلاً به سرو کردن نرسد - بدون هیچ پیامی که علتش را بگوید.
    /// </summary>
    [Fact]
    public void An_unfilled_connection_string_placeholder_is_rejected()
    {
        var settings = ValidSettings();
        settings["ConnectionStrings:DefaultConnection"] =
            "Server=<SQL-SERVER-HOST>;Database=HSEQDb;Trusted_Connection=True";

        var error = Validate(settings, Environments.Production);

        Assert.NotNull(error);
        Assert.Contains("ConnectionStrings:DefaultConnection", error!.Message);
    }

    [Fact]
    public void An_unfilled_sql_password_placeholder_is_rejected()
    {
        var settings = ValidSettings();
        settings["ConnectionStrings:DefaultConnection"] =
            "Server=sql01;Database=HSEQDb;User Id=<SQL-LOGIN>;Password=<SQL-PASSWORD>;TrustServerCertificate=True";

        Assert.NotNull(Validate(settings, Environments.Production));
    }

    /// <summary>
    /// قالبِ واقعیِ مخزن، بدون هیچ تغییری، باید رد شود - وگرنه «کپی کن و اجرا کن»
    /// یک نصبِ ناامن می‌سازد.
    /// </summary>
    [Fact]
    public void The_shipped_template_values_never_start_the_application()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=<SQL-SERVER-HOST>;Database=HSEQDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true",
            ["Jwt:Key"] = "<NEW-BASE64-SIGNING-KEY-AT-LEAST-32-CHARS>",
            ["Jwt:Issuer"] = "OdccPM",
            ["Jwt:Audience"] = "Audience",
            ["Jwt:ExpiryInMinutes"] = "60",
            ["UploadPath"] = @"C:\ApplicationData\HSEQ\Documents\",
            ["UserManagementAPI:Url"] = "http://172.17.0.254:8030/api/",
            ["Cors:AllowedOrigins:0"] = "https://hseq.odcc.local",
        };

        Assert.NotNull(Validate(settings, Environments.Production));
    }

    /// <summary>
    /// جای‌نگهدار در محیط توسعه هم رد می‌شود: یک فایل تنظیماتِ نیمه‌کاره همان‌جا هم
    /// چیزی جز سردرگمی نیست.
    /// </summary>
    [Fact]
    public void Placeholders_are_rejected_in_development_too()
    {
        var settings = ValidSettings();
        settings["UploadPath"] = @"<UPLOAD-PATH>";

        Assert.NotNull(Validate(settings, Environments.Development));
    }

    /// <summary>
    /// نباید آن‌قدر مشتاق باشد که مقدارِ واقعی را جای‌نگهدار بشمارد. یک رمز عبور
    /// می‌تواند به‌طور کاملاً قانونی شامل &lt; و &gt; باشد.
    /// </summary>
    [Theory]
    [InlineData("Server=sql01;Database=HSEQDb;User Id=app;Password=p<a>ss!word;TrustServerCertificate=True")]
    [InlineData("Server=sql01;Database=HSEQDb;User Id=app;Password=a<b;TrustServerCertificate=True")]
    [InlineData("Server=sql01;Database=HSEQDb;User Id=app;Password=x<lower-case>y;TrustServerCertificate=True")]
    public void Real_values_that_merely_contain_angle_brackets_are_accepted(string connectionString)
    {
        var settings = ValidSettings();
        settings["ConnectionStrings:DefaultConnection"] = connectionString;

        Assert.Null(Validate(settings, Environments.Production));
    }

    /// <summary>بررسی‌های قبلی باید سر جایشان بمانند.</summary>
    [Fact]
    public void The_leaked_development_key_is_still_rejected()
    {
        var settings = ValidSettings();
        settings["Jwt:Key"] = "DEVELOPMENT-ONLY-KEY-NOT-VALID-IN-PRODUCTION-0001";

        Assert.NotNull(Validate(settings, Environments.Production));
    }

    [Fact]
    public void A_missing_required_key_is_still_rejected()
    {
        var settings = ValidSettings();
        settings["UserManagementAPI:Url"] = "";

        var error = Validate(settings, Environments.Production);

        Assert.NotNull(error);
        Assert.Contains("UserManagementAPI:Url", error!.Message);
    }

    /// <summary>
    /// پیام خطا باید نام کلید را بگوید و مقدارش را نه - این پیام در لاگ راه‌اندازی
    /// می‌نشیند و لاگ جای رشته‌ی اتصال یا کلید امضا نیست.
    /// </summary>
    [Fact]
    public void The_error_message_names_the_key_but_never_prints_the_value()
    {
        var settings = ValidSettings();
        settings["ConnectionStrings:DefaultConnection"] =
            "Server=sql01;Database=HSEQDb;User Id=app;Password=super-secret-value;TrustServerCertificate=True";
        settings["Jwt:Key"] = "<NEW-BASE64-SIGNING-KEY-AT-LEAST-32-CHARS>";

        var error = Validate(settings, Environments.Production);

        Assert.NotNull(error);
        Assert.DoesNotContain("super-secret-value", error!.Message);
        Assert.DoesNotContain("Password=", error.Message);
    }
}
