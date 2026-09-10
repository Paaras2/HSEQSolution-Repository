using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// قرارداد عمومیِ API. هر آزمون این کلاس دقیقاً همان نشانی‌ای را صدا می‌زند که مرورگر
/// صدا می‌زند، و در هر سه چیدمان میزبانی تکرار می‌شود.
///
/// چرا این‌ها لازم شدند: پیشوند ‎/api‎ دو مالک داشت - هم IIS آن را به‌عنوان مسیرِ
/// Application برمی‌داشت و هم کنترلرها دوباره در ‎[Route]‎ می‌نوشتندش. یک میان‌افزار
/// این تناقض را در زمان اجرا جبران می‌کرد و درست کار کردنش به ترتیبِ آن میان‌افزار و
/// ‎UseRouting‎ وابسته بود. وقتی آن ترتیب به‌هم خورد، روی سرور ‎/api/Auth/login‎ خطای
/// ۴۰۴ داد و ‎/api/api/Auth/login‎ کار کرد - یعنی ورودِ هیچ کاربری ممکن نبود.
/// هیچ آزمونی جلوی آن را نگرفت چون هیچ آزمونی وجود نداشت.
/// </summary>
public class ApiRoutingContractTests
{
    public static TheoryData<HostingModel> HostingModels() => new()
    {
        HostingModel.KestrelAtRoot,
        HostingModel.IisChildApplicationAtApi,
        HostingModel.IisChildApplicationAtOtherPath,
    };

    // -----------------------------------------------------------------------
    // قرارداد اصلی: مسیر ورود
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Login_endpoint_answers_at_the_public_prefix(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{factory.PublicPrefix}/Auth/login", ValidCredentials());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadTokenAsync(response)));
    }

    /// <summary>
    /// مسیر دوتایی نباید جزو API عمومی باشد. این دقیقاً همان چیزی است که روی سرور
    /// «کار می‌کرد» و به‌اشتباه شبیه راه‌حل به نظر می‌رسید.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Doubled_prefix_is_not_part_of_the_public_api(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"{factory.PublicPrefix}/api/Auth/login", ValidCredentials());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// همان توکن، این بار روی مسیرِ دوتایی. اگر روزی مسیریابی دوباره پیشوند را داخل
    /// خودش بگیرد، این آزمون هم‌زمان با آزمون ورود می‌شکند و علتش صریح است.
    ///
    /// (اینکه اکشن‌ها روی خودِ ریشه‌ی مبدأ - ‎/Auth/login‎ بدون پیشوند - هم جواب
    /// می‌دهند عمدی است و بیرون از IIS دیده نمی‌شود: IIS فقط درخواست‌های زیر مسیر
    /// Application را به این برنامه می‌دهد، پس چنین نشانی‌ای اصلاً به بک‌اند نمی‌رسد
    /// و به قاعده‌ی بازگشتِ SPA می‌خورد.)
    /// </summary>
    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Doubled_prefix_is_not_reachable_even_with_a_valid_token(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var login = await client.PostAsync($"{factory.PublicPrefix}/Auth/login", ValidCredentials());
        var token = await ReadTokenAsync(login);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{factory.PublicPrefix}/api/Admin/Get?pcode=3256");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // PathBase دست‌نخورده می‌ماند
    // -----------------------------------------------------------------------

    /// <summary>
    /// نسخه‌ی قبلی پیشوند را از ‎PathBase‎ به ‎Path‎ منتقل می‌کرد و ‎PathBase‎ را خالی
    /// می‌گذاشت. نتیجه‌اش این بود که هر نشانی‌ای که برنامه تولید می‌کرد - ریدایرکت،
    /// هدر ‎Location‎، نشانی سرورِ Swagger - پیشوند ‎/api‎ را نداشت و به ریشه‌ی سایتِ
    /// کلاینت اشاره می‌کرد. این آزمون همان را می‌بندد.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Host_declared_path_base_is_preserved_for_link_generation(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        await client.PostAsync($"{factory.PublicPrefix}/Auth/login", ValidCredentials());

        var recorder = factory.Services.GetRequiredService<RequestPathRecorder>();
        Assert.Equal(factory.PublicPrefix, recorder.PathBase);
        Assert.Equal("/Auth/login", recorder.Path);
    }

    // -----------------------------------------------------------------------
    // مسیرهای نماینده: بدون احراز هویت، با احراز هویت
    // -----------------------------------------------------------------------

    /// <summary>
    /// ‎Health‎ عمداً بدون احراز هویت است. اینجا دیتابیس در دسترس نیست، پس پاسخ درست
    /// ۵۰۳ است - که خودش ثابت می‌کند اکشن اجرا شده (نه ۴۰۴ و نه ۴۰۱).
    /// </summary>
    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Health_is_anonymous_and_routed(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{factory.PublicPrefix}/Health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", payload.GetProperty("status").GetString());
    }

    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Doubled_prefix_on_health_is_not_routed(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{factory.PublicPrefix}/api/Health");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// مسیر محافظت‌شده بدون توکن باید ۴۰۱ بدهد، نه ۴۰۴. تفاوتشان مهم است: ۴۰۴ یعنی
    /// مسیریابی شکسته، ۴۰۱ یعنی مسیریابی درست است و احراز هویت کار می‌کند.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Protected_route_without_a_token_is_unauthorized_not_missing(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{factory.PublicPrefix}/Admin/Get?pcode=3256");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// جریان کامل: ورود، گرفتن توکن، و صدا زدن یک مسیر محافظت‌شده با همان توکن روی
    /// همان پیشوند عمومی.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostingModels))]
    public async Task Token_from_login_authorizes_a_protected_route_on_the_same_prefix(HostingModel hostingModel)
    {
        using var factory = new HseqApiFactory(hostingModel);
        using var client = factory.CreateClient();

        var login = await client.PostAsync($"{factory.PublicPrefix}/Auth/login", ValidCredentials());
        var token = await ReadTokenAsync(login);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{factory.PublicPrefix}/Admin/Get?pcode=3256");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", await response.Content.ReadAsStringAsync());
    }

    // -----------------------------------------------------------------------
    // کمکی‌ها
    // -----------------------------------------------------------------------

    internal static FormUrlEncodedContent ValidCredentials() => new(new Dictionary<string, string>
    {
        ["Username"] = StubUmService.ValidUsername,
        ["Password"] = StubUmService.ValidPassword,
    });

    internal static async Task<string?> ReadTokenAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("token").GetString();
    }
}
