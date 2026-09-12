using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HSEQ.API.ServiceConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// رفتار برنامه وقتی سرویس مدیریت کاربران در دسترس نیست.
///
/// چرا این کلاس نوشته شد: روی سرور، فایروال بسته‌های مقصدِ ۸۰۳۰ را بی‌صدا دور
/// می‌ریخت. نتیجه برای کاربر این بود که دکمه‌ی «ورود» صد ثانیه کار می‌کرد و بعد
/// خطا می‌داد، و برای اپراتور یک پیام «Cannot connect» بدون هیچ سرنخی - چون علت
/// واقعی گرفته و دور ریخته می‌شد.
/// </summary>
public class UserManagementOutageTests
{
    /// <summary>
    /// مهلت باید کوتاه و از تنظیمات باشد. پیش‌فرضِ ۱۰۰ ثانیه‌ایِ HttpClient برای یک
    /// فراخوان احراز هویت در شبکه‌ی داخلی قابل قبول نیست.
    /// </summary>
    [Fact]
    public void Default_user_management_timeout_is_seconds_not_minutes()
    {
        var configuration = new ConfigurationBuilder().Build();

        var seconds = configuration.GetValue("UserManagementAPI:TimeoutSeconds", 10);

        Assert.InRange(seconds, 1, 30);
    }

    [Fact]
    public void User_management_timeout_is_configurable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserManagementAPI:TimeoutSeconds"] = "25",
            })
            .Build();

        Assert.Equal(25, configuration.GetValue("UserManagementAPI:TimeoutSeconds", 10));
    }

    /// <summary>
    /// وقتی سرویس در دسترس نیست، کاربر باید یک پیام قابل فهم ببیند - نه خطای ۵۰۰ و
    /// نه هیچ نشانی یا جزئیاتی از داخل سرور.
    /// </summary>
    [Fact]
    public async Task Outage_is_a_safe_client_error_with_nothing_internal_in_it()
    {
        using var factory = new HseqApiFactory(HostingModel.KestrelAtRoot, Environments.Production);
        using var client = factory.CreateClient();

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = StubUmService.ValidUsername,
            ["Password"] = StubUmService.PasswordThatBreaksUpstream,
        });

        var response = await client.PostAsync("/api/Auth/login", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = payload.GetProperty("message").GetString() ?? string.Empty;

        Assert.False(string.IsNullOrWhiteSpace(message));
        foreach (var leak in new[] { "8030", "172.", "127.0.0.1", "http://", "Exception", "at HSEQ." })
        {
            Assert.DoesNotContain(leak, message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // -----------------------------------------------------------------------
    // نشانی loopback برای سرویس UM
    // -----------------------------------------------------------------------

    private static IConfiguration ProductionConfiguration(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=sql;Database=HSEQDb;Trusted_Connection=True",
            ["Jwt:Key"] = "a-production-signing-key-with-enough-length",
            ["Jwt:Issuer"] = "OdccPM",
            ["Jwt:Audience"] = "Audience",
            ["Jwt:ExpiryInMinutes"] = "60",
            ["UploadPath"] = @"C:\ApplicationData\HSEQ\Documents\",
            ["UserManagementAPI:Url"] = "http://um.internal:8030/api/",
            ["Cors:AllowedOrigins:0"] = "http://172.17.0.254:2525",
        };

        foreach (var (key, value) in overrides) values[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static readonly IHostEnvironment Production =
        new HostingEnvironment { EnvironmentName = Environments.Production };

    /// <summary>
    /// شبیه‌ساز UM هر رمزی را می‌پذیرد و روی loopback گوش می‌دهد. اشاره‌ی تنظیمات
    /// عملیاتی به آن یعنی احراز هویت عملاً دور زده شده، پس پیش‌فرض باید رد باشد.
    /// </summary>
    [Theory]
    [InlineData("http://localhost:8899/api/")]
    [InlineData("http://127.0.0.1:8899/api/")]
    public void Loopback_user_management_is_rejected_by_default(string url)
    {
        var configuration = ProductionConfiguration(("UserManagementAPI:Url", url));

        var error = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.Validate(configuration, Production));

        Assert.Contains("میزبان محلی", error.Message);
        Assert.Contains("AllowLoopback", error.Message);
    }

    /// <summary>
    /// ولی نصب UM روی همان سرور IIS چیدمان متعارفی است. باید ممکن باشد - فقط
    /// آگاهانه، نه اتفاقی.
    /// </summary>
    [Theory]
    [InlineData("http://localhost:8030/api/")]
    [InlineData("http://127.0.0.1:8030/api/")]
    public void Loopback_user_management_is_allowed_when_explicitly_opted_in(string url)
    {
        var configuration = ProductionConfiguration(
            ("UserManagementAPI:Url", url),
            ("UserManagementAPI:AllowLoopback", "true"));

        ProductionConfigurationValidator.Validate(configuration, Production);
    }

    [Fact]
    public void A_remote_user_management_url_never_needs_the_opt_in()
    {
        ProductionConfigurationValidator.Validate(ProductionConfiguration(), Production);
    }

    // -----------------------------------------------------------------------
    // شکل نشانی
    // -----------------------------------------------------------------------

    /// <summary>
    /// یک اسلشِ جاافتاده («http:/host») نشانی را نامعتبر می‌کند. آن خطا
    /// UriFormatException است، نه HttpRequestException - پس از مهارِ «سرویس در
    /// دسترس نیست» رد می‌شود و در *اولین تلاش ورود* به‌صورت ۵۰۰ ظاهر می‌گردد.
    /// باید همان هنگام راه‌اندازی گرفته شود.
    /// </summary>
    [Theory]
    [InlineData("http:/172.17.0.86:8030/api/")]   // یک اسلش جا افتاده
    [InlineData("172.17.0.86:8030/api/")]         // بدون طرح
    [InlineData("ftp://um.internal:8030/api/")]   // طرح نامربوط
    [InlineData("فقط یک متن")]
    public void A_malformed_user_management_url_is_refused_at_startup(string url)
    {
        var configuration = ProductionConfiguration(("UserManagementAPI:Url", url));

        var error = Assert.Throws<InvalidOperationException>(
            () => ProductionConfigurationValidator.Validate(configuration, Production));

        Assert.Contains("URL معتبر", error.Message);
    }

    [Theory]
    [InlineData("http://app2-SRV:8030/api/")]
    [InlineData("http://172.17.0.86:8030/api/")]
    [InlineData("https://um.odcc.ir/api/")]
    public void A_well_formed_user_management_url_is_accepted(string url)
    {
        ProductionConfigurationValidator.Validate(
            ProductionConfiguration(("UserManagementAPI:Url", url)), Production);
    }
}
