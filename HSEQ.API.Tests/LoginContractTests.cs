using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// قرارداد خودِ ورود - جدا از مسیریابی. مسیر درست بودن یعنی درخواست به اکشن می‌رسد؛
/// این کلاس می‌گوید وقتی رسید چه اتفاقی باید بیفتد.
/// </summary>
public class LoginContractTests
{
    private const string PublicLoginPath = "/api/Auth/login";

    /// <summary>
    /// شکلِ دقیقی که کلاینت می‌فرستد: ‎POST‎ با ‎application/x-www-form-urlencoded‎ و
    /// دو فیلد ‎Username‎/‎Password‎ - همان چیزی که ‎authApi.login‎ می‌سازد.
    /// </summary>
    [Fact]
    public async Task Client_form_encoded_payload_is_accepted_as_written()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi);
        using var client = factory.CreateClient();

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = StubUmService.ValidUsername,
            ["Password"] = StubUmService.ValidPassword,
        });

        var response = await client.PostAsync(PublicLoginPath, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ApiRoutingContractTests.ReadTokenAsync(response)));
    }

    [Fact]
    public async Task Wrong_password_is_a_client_error_not_a_server_error()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi);
        using var client = factory.CreateClient();

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = StubUmService.ValidUsername,
            ["Password"] = "not-the-password",
        });

        var response = await client.PostAsync(PublicLoginPath, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // پیام برای کاربر است و کلاینت همین را نمایش می‌دهد؛ نباید جای آن جزئیات
        // داخلی سرور بنشیند.
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = payload.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
        AssertNothingSensitive(message!);
    }

    [Fact]
    public async Task Empty_submission_is_a_client_error_not_a_server_error()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi);
        using var client = factory.CreateClient();

        var response = await client.PostAsync(PublicLoginPath, new FormUrlEncodedContent([]));

        Assert.True((int)response.StatusCode is >= 400 and < 500,
            $"ورودی خالی باید خطای سمت کلاینت بدهد، ولی {(int)response.StatusCode} برگشت.");
        AssertNothingSensitive(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// وقتی سرویس UM در دسترس نیست، کاربر باید پیام «بعداً تلاش کنید» ببیند - نه
    /// خطای ۵۰۰ و نه هیچ نشانی داخلی.
    /// </summary>
    [Fact]
    public async Task Unreachable_user_management_service_does_not_surface_as_a_500()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi);
        using var client = factory.CreateClient();

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = StubUmService.ValidUsername,
            ["Password"] = StubUmService.PasswordThatBreaksUpstream,
        });

        var response = await client.PostAsync(PublicLoginPath, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNothingSensitive(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// همان قرارداد، این بار با pipeline محیط عملیاتی - که ‎UseHttpsRedirection‎ و
    /// اعتبارسنجی سخت‌گیرانه‌ی تنظیمات را هم شامل می‌شود و Swagger را می‌بندد.
    /// </summary>
    [Fact]
    public async Task Production_pipeline_serves_the_same_login_contract()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi, Environments.Production);
        using var client = factory.CreateClient();

        var response = await client.PostAsync(PublicLoginPath, ApiRoutingContractTests.ValidCredentials());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ApiRoutingContractTests.ReadTokenAsync(response)));
    }

    /// <summary>
    /// میان‌برِ ورود توسعه‌دهنده نباید بیرون از محیط توسعه وجود داشته باشد.
    /// </summary>
    [Fact]
    public async Task Dev_login_shortcut_is_absent_in_production()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi, Environments.Production);
        using var client = factory.CreateClient();

        var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["Role"] = "Admin" });
        var response = await client.PostAsync("/api/Auth/dev-login", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// مستندات API در محیط عملیاتی نباید سرو شود.
    /// </summary>
    [Fact]
    public async Task Swagger_is_closed_in_production()
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi, Environments.Production);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void AssertNothingSensitive(string body)
    {
        string[] forbidden =
        [
            "Server=", "Password=", "User Id=", "ConnectionString",
            "at HSEQ.", "StackTrace", "Exception:",
        ];

        foreach (var needle in forbidden)
        {
            Assert.DoesNotContain(needle, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
