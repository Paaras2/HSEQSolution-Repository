using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// چیدمان تک‌سایتی: همین برنامه هم API را سرو می‌کند و هم فایل‌های build شده‌ی
/// کلاینت را از wwwroot. در IIS فقط یک سایت لازم است و Application جداگانه‌ای زیر
/// ‎/api‎ وجود ندارد.
///
/// آنچه اینجا بسته می‌شود، همان چیزی است که در چیدمان دو-پوشه‌ای با یک شرط در
/// web.config تأمین می‌شد و حالا باید در کد تأمین شود: مسیرهای سمت کلاینت
/// index.html بگیرند، ولی مسیرهای API هرگز - وگرنه کلاینتی که منتظر JSON است
/// یک صفحه‌ی HTML می‌گیرد و خطایش هیچ ربطی به علت واقعی ندارد.
/// </summary>
public class SingleSiteHostingTests : IDisposable
{
    private readonly string _webRoot;
    private readonly List<HseqApiFactory> _factories = [];

    public SingleSiteHostingTests()
    {
        // یک wwwroot واقعی روی دیسک، چون تشخیصِ برنامه بر پایه‌ی وجود همین فایل است.
        _webRoot = Path.Combine(Path.GetTempPath(), "hseq-wwwroot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"),
            "<!doctype html><html><body><div id=\"root\"></div></body></html>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('hseq')");
    }

    public void Dispose()
    {
        foreach (var factory in _factories) factory.Dispose();
        try { Directory.Delete(_webRoot, recursive: true); } catch { /* پاک‌سازی بهترین‌تلاش */ }
    }

    // WithWebHostBuilder یک نمونه‌ی delegating برمی‌گرداند، نه HseqApiFactory - پس
    // نوع بازگشتی همان پایه است. خودِ HseqApiFactory هم باید زنده بماند تا میزبان
    // پیش از پایان آزمون از بین نرود.
    private WebApplicationFactory<Program> CreateFactory(string webRoot)
    {
        var factory = new HseqApiFactory(HostingModel.KestrelAtRoot, Environments.Production);
        _factories.Add(factory);
        return factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));
    }

    private WebApplicationFactory<Program> CreateFactory() => CreateFactory(_webRoot);

    // -----------------------------------------------------------------------
    // کلاینت
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Site_root_serves_the_client()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// مسیرهای React Router روی دیسک فایلی ندارند؛ باید index.html بگیرند تا رفرش
    /// کردن و باز کردن مستقیمِ لینک کار کند.
    /// </summary>
    [Theory]
    [InlineData("/documents")]
    [InlineData("/admin")]
    [InlineData("/login")]
    [InlineData("/documents/42/revisions")]
    public async Task Client_side_routes_fall_back_to_index(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Hashed_assets_are_served_as_real_files()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hseq", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// نام index.html با هر انتشار عوض نمی‌شود، پس اگر مرورگر کشش کند همچنان به
    /// فایل‌های هش‌دارِ قدیمی ارجاع می‌دهد که دیگر وجود ندارند - و کاربر صفحه‌ی سفید
    /// می‌بیند بدون آنکه کاری کرده باشد.
    /// </summary>
    [Fact]
    public async Task Index_is_never_cached_but_hashed_assets_are()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var index = await client.GetAsync("/");
        var asset = await client.GetAsync("/assets/index-abc123.js");

        Assert.Contains("no-cache", index.Headers.CacheControl?.ToString() ?? string.Empty);
        Assert.Contains("max-age=31536000", asset.Headers.CacheControl?.ToString() ?? string.Empty);
    }

    // -----------------------------------------------------------------------
    // API - قرارداد عمومی دست‌نخورده می‌ماند
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Login_still_answers_at_the_public_prefix()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/Auth/login", ApiRoutingContractTests.ValidCredentials());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ApiRoutingContractTests.ReadTokenAsync(response)));
    }

    [Fact]
    public async Task Doubled_prefix_is_still_not_part_of_the_public_api()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/api/Auth/login", ApiRoutingContractTests.ValidCredentials());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// مهم‌ترین آزمون این کلاس: مسیر ناموجودِ API باید ۴۰۴ بماند، نه اینکه
    /// index.html بگیرد. اگر بازگشتِ SPA این را هم بگیرد، هر خطای API به یک صفحه‌ی
    /// HTML تبدیل می‌شود و کلاینت هنگام JSON.parse خطایی می‌دهد که هیچ ربطی به
    /// علت واقعی ندارد.
    /// </summary>
    [Theory]
    [InlineData("/api/NoSuchController")]
    [InlineData("/api/Auth/no-such-action")]
    [InlineData("/api/api/Health")]
    public async Task Unknown_api_paths_stay_404_and_never_return_html(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<div id=\"root\">", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// مسیر محافظت‌شده باید ۴۰۱ بدهد - نه ۴۰۴ و نه صفحه‌ی ورودِ HTML.
    /// </summary>
    [Fact]
    public async Task Protected_api_route_still_returns_401_not_the_client_page()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/MasterData/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("<div id=\"root\">", await response.Content.ReadAsStringAsync());
    }

    // -----------------------------------------------------------------------
    // آنچه نباید از راه وب دیده شود
    // -----------------------------------------------------------------------

    /// <summary>
    /// در چیدمان تک‌سایتی، ‎appsettings.Production.json‎ و DLLها در ریشه‌ی محتوای
    /// برنامه‌اند - یعنی *کنار* پوشه‌ی سایت، نه داخلش. فقط wwwroot سرو می‌شود.
    ///
    /// این را صریح می‌بندیم چون آن فایل کلید امضای JWT و رشته‌ی اتصال را دارد؛
    /// قابل دانلود شدنش یعنی هر کسی می‌تواند توکن جعلی بسازد.
    /// </summary>
    [Theory]
    [InlineData("/appsettings.Production.json")]
    [InlineData("/appsettings.json")]
    [InlineData("/HSEQ.API.dll")]
    [InlineData("/web.config")]
    [InlineData("/../appsettings.Production.json")]
    public async Task Application_files_are_not_downloadable(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        // بازگشتِ SPA ممکن است index.html بدهد؛ چیزی که اهمیت دارد این است که
        // *محتوای فایل واقعی* بیرون نرود.
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ConnectionStrings", body);
        Assert.DoesNotContain("DefaultConnection", body);
        Assert.DoesNotContain("aspNetCore", body);
    }

    // -----------------------------------------------------------------------
    // چیدمان فقط-API همچنان کار می‌کند
    // -----------------------------------------------------------------------

    /// <summary>
    /// بدون wwwroot/index.html، همان باینری دقیقاً مثل قبل فقط API است. یعنی یک
    /// بسته هر دو چیدمان را پشتیبانی می‌کند و ریشه‌ی سایت چیزی برای سرو کردن ندارد.
    /// </summary>
    [Fact]
    public async Task Without_a_client_build_the_app_serves_api_only()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), "hseq-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        try
        {
            using var factory = CreateFactory(emptyRoot);
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsync("/api/Auth/login", ApiRoutingContractTests.ValidCredentials())).StatusCode);
        }
        finally
        {
            try { Directory.Delete(emptyRoot, recursive: true); } catch { /* پاک‌سازی بهترین‌تلاش */ }
        }
    }
}
