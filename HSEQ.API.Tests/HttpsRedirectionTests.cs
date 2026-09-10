using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// هدایت به HTTPS باید تصمیمی صریح باشد، نه نتیجه‌ی جانبیِ نام محیط.
///
/// چرا: سایت روی یک بایندینگ HTTP سرو می‌شود. با روشن بودنِ بی‌قیدِ میان‌افزار،
/// به‌ازای هر درخواست یک هشدار «Failed to determine the https port for redirect»
/// در لاگ می‌نشست - و اگر روزی بایندینگ HTTPS اضافه می‌شد، رفتار برنامه بدون هیچ
/// تغییری در کد یا تنظیمات به هدایت اجباری عوض می‌شد.
/// </summary>
public class HttpsRedirectionTests
{
    /// <summary>
    /// پیش‌فرضِ محیط عملیاتی: بدون هدایت. همان رفتاری که چیدمان فعلی (HTTP روی
    /// پورت اختصاصی) در عمل داشت، ولی این‌بار بدون هشدارِ هر-درخواست.
    /// </summary>
    [Fact]
    public async Task Production_does_not_redirect_unless_it_is_turned_on()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi, Environments.Production);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/Health");

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// و وقتی روشن شود، واقعاً هدایت می‌کند - به همان پورتی که اعلام شده، نه به
    /// پورتی که میان‌افزار حدس بزند.
    /// </summary>
    [Fact]
    public async Task Turning_it_on_redirects_to_the_configured_https_port()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi, Environments.Production);
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Https:RedirectToHttps", "true");
            builder.UseSetting("Https:Port", "8443");
        });
        using var client = configured.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/Health");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("https", location!.Scheme);
        Assert.Equal(8443, location.Port);

        // مهم‌تر از خودِ هدایت: پیشوند عمومی در نشانی مقصد باقی می‌ماند. با پاک شدنِ
        // PathBase - رفتار نسخه‌ی قبل - کاربر به ریشه‌ی سایت کلاینت هدایت می‌شد.
        Assert.Equal("/api/Health", location.AbsolutePath);
    }
}

/// <summary>
/// پیشوند از تنظیمات می‌آید، و شکل‌های نزدیک‌به‌درستِ همان مقدار نباید برنامه را
/// بخوابانند یا مسیر را عوض کنند.
/// </summary>
public class ApiPathBaseConfigurationTests
{
    [Theory]
    [InlineData("/api")]
    [InlineData("api")]      // اسلشِ ابتدایی جا افتاده
    [InlineData("/api/")]    // اسلشِ اضافه در انتها
    [InlineData("  /api  ")] // فاصله‌ی اضافه
    public async Task Near_miss_spellings_of_the_prefix_still_serve_the_same_contract(string configured)
    {
        using var factory = new HseqApiFactory(HostingModel.KestrelAtRoot);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Api:PathBase", configured));
        using var client = configuredFactory.CreateClient();

        var response = await client.PostAsync("/api/Auth/login", ApiRoutingContractTests.ValidCredentials());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// خالی گذاشتنش یعنی «برنامه پیشوند نگذارد» - برای وقتی که میزبان دیگری آن را
    /// می‌گذارد. کنترلرها آن‌وقت روی ریشه‌ی مبدأ جواب می‌دهند.
    /// </summary>
    [Fact]
    public async Task An_empty_prefix_means_the_application_adds_none()
    {
        using var factory = new HseqApiFactory(HostingModel.KestrelAtRoot);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Api:PathBase", ""));
        using var client = configuredFactory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync("/api/Auth/login", ApiRoutingContractTests.ValidCredentials())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync("/Auth/login", ApiRoutingContractTests.ValidCredentials())).StatusCode);
    }
}
