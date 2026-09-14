using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// پاسخِ ورود، از دیدِ صفحه‌ی ورود.
///
/// ‎LoginContractTests‎ می‌گوید ورود کار می‌کند؛ این کلاس می‌گوید صفحه‌ی ورود از پاسخ
/// چه چیزی می‌تواند بفهمد: کاربر کیست، رمز غلط بوده یا سامانه‌ی کاربران در دسترس
/// نبوده، حساب غیرفعال است یا هنوز رمز پیش‌فرض دارد - و چه چیزی هرگز نباید به
/// مرورگر برسد.
/// </summary>
public class LoginResponseTests
{
    private const string LoginPath = "/api/Auth/login";

    private static async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> PostJsonAsync(object payload)
    {
        using var factory = new HseqApiFactory(HostingModel.IisChildApplicationAtApi);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath, payload);
        var raw = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(raw);
        return (response.StatusCode, document.RootElement.Clone(), raw);
    }

    private static object Valid(string? username = null, string? password = null) => new
    {
        username = username ?? StubUmService.ValidUsername,
        password = password ?? StubUmService.ValidPassword,
    };

    // -----------------------------------------------------------------------
    // ورود موفق
    // -----------------------------------------------------------------------

    /// <summary>بدنه‌ای به شکلِ دقیقِ checkCredential: ‎{ "username", "password" }‎ با JSON.</summary>
    [Fact]
    public async Task The_checkCredential_request_shape_signs_in_and_returns_the_profile()
    {
        var (status, body, _) = await PostJsonAsync(Valid());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));

        var user = body.GetProperty("user");
        Assert.Equal(3256, user.GetProperty("pCode").GetInt32());
        Assert.Equal(StubUmService.ValidFirstName, user.GetProperty("firstName").GetString());
        Assert.Equal(StubUmService.ValidLastName, user.GetProperty("lastName").GetString());
        Assert.False(user.GetProperty("isFirstLogin").GetBoolean());
    }

    [Fact]
    public async Task National_code_and_mobile_never_reach_the_browser()
    {
        var (status, _, raw) = await PostJsonAsync(Valid());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain(StubUmService.ValidNationalCode, raw);
        Assert.DoesNotContain(StubUmService.ValidMobile, raw);
        Assert.DoesNotContain("nationalCode", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"mobile\"", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>نام در توکن است تا پس از ری‌لود هم نمایش داده شود.</summary>
    [Fact]
    public async Task The_token_carries_the_users_name()
    {
        var (_, body, _) = await PostJsonAsync(Valid());

        var claims = DecodePayload(body.GetProperty("token").GetString()!);
        Assert.Equal(StubUmService.ValidFirstName, claims.GetProperty("given_name").GetString());
        Assert.Equal(StubUmService.ValidLastName, claims.GetProperty("family_name").GetString());
    }

    [Fact]
    public async Task A_first_login_is_reported_to_the_client()
    {
        var (status, body, _) = await PostJsonAsync(Valid(StubUmService.FirstLoginUsername, StubUmService.FirstLoginPassword));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body.GetProperty("user").GetProperty("isFirstLogin").GetBoolean());
    }

    // -----------------------------------------------------------------------
    // ورودیِ کاربر
    // -----------------------------------------------------------------------

    /// <summary>
    /// صفحه‌کلید فارسی و متنِ کپی‌شده از سند فارسی: ارقام فارسی، نویسه‌ی نامرئیِ جهت و فاصله.
    /// </summary>
    [Theory]
    [InlineData("۳۲۵۶")]
    [InlineData("٣٢٥٦")]
    [InlineData("\u200F3256")]
    [InlineData(" 3256 ")]
    [InlineData("\u202B۳۲۵۶\u202C")]
    public async Task The_username_is_normalized_before_asking_user_management(string typed)
    {
        var (status, _, _) = await PostJsonAsync(Valid(username: typed));

        Assert.Equal(HttpStatusCode.OK, status);
    }

    /// <summary>رمز نباید trim یا تبدیل شود - فاصله می‌تواند بخشی از رمزِ واقعی باشد.</summary>
    [Theory]
    [InlineData(" " + StubUmService.ValidPassword)]
    [InlineData(StubUmService.ValidPassword + " ")]
    public async Task The_password_is_checked_exactly_as_typed(string typed)
    {
        var (status, body, _) = await PostJsonAsync(Valid(password: typed));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_credentials", body.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("", "anything")]
    [InlineData("3256", "")]
    [InlineData("\u200F ", "anything")]
    public async Task Missing_fields_have_their_own_reason(string username, string password)
    {
        var (status, body, _) = await PostJsonAsync(new { username, password });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("missing_fields", body.GetProperty("reason").GetString());
    }

    // -----------------------------------------------------------------------
    // ورود ناموفق
    // -----------------------------------------------------------------------

    /// <summary>پیامِ خودِ سامانه‌ی UM - «اطلاعات وارد شده صحیح نمی باشد» - به کاربر می‌رسد.</summary>
    [Fact]
    public async Task A_wrong_password_carries_the_user_management_message()
    {
        var (status, body, _) = await PostJsonAsync(Valid(password: "not-the-password"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_credentials", body.GetProperty("reason").GetString());
        Assert.Equal(StubUmService.WrongCredentialsMessage, body.GetProperty("message").GetString());
        Assert.False(body.TryGetProperty("token", out _));
    }

    /// <summary>
    /// سامانه‌ی کاربرانِ خاموش نباید «رمز غلط» خوانده شود؛ وگرنه کاربر رمزی را که درست
    /// است بی‌دلیل عوض می‌کند.
    /// </summary>
    [Theory]
    [InlineData(StubUmService.PasswordThatBreaksUpstream)]
    [InlineData(StubUmService.PasswordThatReturnsHtml)]
    public async Task An_unavailable_user_management_service_is_not_reported_as_a_wrong_password(string password)
    {
        var (status, body, _) = await PostJsonAsync(Valid(password: password));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("unavailable", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task An_inactive_account_is_refused_with_its_own_reason()
    {
        var (status, body, _) = await PostJsonAsync(Valid(username: StubUmService.InactiveUsername));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("inactive", body.GetProperty("reason").GetString());
        Assert.False(body.TryGetProperty("token", out _));
    }

    /// <summary>
    /// UM رمز را پذیرفته ولی ساختِ نشست (خواندنِ نقش از HSEQDb) شکست خورده است. پیش از این
    /// مرورگر یک ۵۰۰ِ خالی می‌گرفت و صفحه آن را «سامانه‌ی کاربران پاسخ نمی‌دهد» می‌خواند.
    /// </summary>
    [Fact]
    public async Task A_server_failure_after_user_management_accepts_is_reported_as_such()
    {
        var (status, body, raw) = await PostJsonAsync(Valid(username: StubUmService.BrokenSessionUsername));

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.Equal("server_error", body.GetProperty("reason").GetString());
        Assert.False(body.TryGetProperty("token", out _));
        Assert.DoesNotContain("simulated", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement DecodePayload(string token)
    {
        var part = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        part = part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=');

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(part)));
        return document.RootElement.Clone();
    }
}
