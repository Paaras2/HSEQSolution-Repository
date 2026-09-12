using System.Net;
using System.Text;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Shared.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// خودِ <see cref="UmService"/> با پاسخ‌هایی که سرویس مدیریت کاربران واقعاً ممکن
/// است بدهد - نه یک stub که رفتار را تقلید کند.
///
/// چرا این کلاس لازم شد: سرویس UM پشت یک پروکسی نشسته بود و به‌جای JSON صفحه‌ی
/// HTML برگرداند. ‎ReadFromJsonAsync‎ آن‌جا ‎JsonException‎ داد، و چون نه
/// ‎HttpRequestException‎ بود نه ‎TaskCanceledException‎، از هر دو مهار رد شد و
/// کاربر «Internal server error» با کد ۵۰۰ گرفت - خطایی که هیچ اشاره‌ای به علت
/// نداشت.
/// </summary>
public class UmServiceResponseTests
{
    /// <summary>هر پاسخی که بخواهیم، بدون نیاز به شبکه.</summary>
    private sealed class CannedResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly string _contentType;

        public CannedResponseHandler(HttpStatusCode status, string body, string contentType)
        {
            _status = status;
            _body = body;
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, _contentType)
            });
    }

    private static UmService CreateService(HttpStatusCode status, string body, string contentType = "application/json")
    {
        // UMUrl از یک وضعیت ایستا خوانده می‌شود؛ اینجا صریح مقداردهی می‌شود تا آزمون
        // به ترتیب اجرا وابسته نباشد.
        AppSettingFactory.Initialize(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserManagementAPI:Url"] = "http://um.internal:8030/api/",
                ["Jwt:ExpiryInMinutes"] = "60",
            }).Build());

        var client = new HttpClient(new CannedResponseHandler(status, body, contentType));
        return new UmService(client, NullLogger<UmService>.Instance);
    }

    private static LoginRequestModel AnyCredentials() =>
        new() { Username = "3256", Password = "whatever" };

    // -----------------------------------------------------------------------
    // پاسخ‌هایی که JSON نیستند
    // -----------------------------------------------------------------------

    /// <summary>
    /// دقیقاً همان چیزی که روی سرور دیده شد: کد ۲۰۰، ولی بدنه HTML.
    /// </summary>
    [Fact]
    public async Task An_html_success_body_becomes_a_handled_error_not_a_json_exception()
    {
        var service = CreateService(HttpStatusCode.OK,
            "<!DOCTYPE html><html><head><title>Sign In</title></head><body>...</body></html>",
            "text/html");

        var error = await Assert.ThrowsAsync<ExternalAuthException>(
            () => service.CheckUserAndPassword(AnyCredentials()));

        Assert.Equal(502, error.Code);
        AssertSafeForTheBrowser(error.Message);
    }

    /// <summary>
    /// پاسخ خطا هم ممکن است JSON نباشد - صفحه‌ی خطای IIS نمونه‌ی متداولش است.
    /// </summary>
    [Fact]
    public async Task An_html_error_body_becomes_a_handled_error_too()
    {
        var service = CreateService(HttpStatusCode.BadGateway,
            "<html><head><title>502 - Web server received an invalid response</title></head></html>",
            "text/html");

        var error = await Assert.ThrowsAsync<ExternalAuthException>(
            () => service.CheckUserAndPassword(AnyCredentials()));

        Assert.Equal(502, error.Code);
        AssertSafeForTheBrowser(error.Message);
    }

    [Fact]
    public async Task An_empty_body_becomes_a_handled_error()
    {
        var service = CreateService(HttpStatusCode.OK, string.Empty);

        var error = await Assert.ThrowsAsync<ExternalAuthException>(
            () => service.CheckUserAndPassword(AnyCredentials()));

        AssertSafeForTheBrowser(error.Message);
    }

    // -----------------------------------------------------------------------
    // پاسخ‌های درست - رفتار قبلی باید دست‌نخورده بماند
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_valid_credential_response_is_returned()
    {
        var service = CreateService(HttpStatusCode.OK,
            """{"pCode":3256,"isActive":true,"firstName":"کاربر","lastName":"آزمایشی"}""");

        var credential = await service.CheckUserAndPassword(AnyCredentials());

        Assert.NotNull(credential);
        Assert.Equal(3256, credential.PCode);
        Assert.True(credential.IsActive);
    }

    /// <summary>
    /// پیامِ خودِ سرویس UM باید به کاربر برسد - همان «نام کاربری یا رمز عبور
    /// نامعتبر است» که کاربر انتظار دارد، نه یک متن عمومی.
    /// </summary>
    [Fact]
    public async Task A_json_error_response_keeps_the_upstream_message_and_code()
    {
        var service = CreateService(HttpStatusCode.Unauthorized,
            """{"message":"نام کاربری یا رمز عبور نامعتبر است","code":401}""");

        var error = await Assert.ThrowsAsync<ExternalAuthException>(
            () => service.CheckUserAndPassword(AnyCredentials()));

        Assert.Equal(401, error.Code);
        Assert.Contains("نام کاربری", error.Message);
    }

    /// <summary>
    /// وقتی بدنه JSON است ولی کدی ندارد، کد وضعیتِ خودِ HTTP گویاترین چیزی است
    /// که در اختیار داریم.
    /// </summary>
    [Fact]
    public async Task A_json_error_without_a_code_falls_back_to_the_http_status()
    {
        var service = CreateService(HttpStatusCode.ServiceUnavailable,
            """{"message":"سرویس موقتاً در دسترس نیست"}""");

        var error = await Assert.ThrowsAsync<ExternalAuthException>(
            () => service.CheckUserAndPassword(AnyCredentials()));

        Assert.Equal(503, error.Code);
    }

    private static void AssertSafeForTheBrowser(string message)
    {
        // پیام مستقیم به صفحه‌ی ورود می‌رود. نه نشانی داخلی، نه بدنه‌ی پاسخ، نه
        // نام استثنا.
        foreach (var leak in new[] { "<", "um.internal", "8030", "http://", "Exception", "Json" })
        {
            Assert.DoesNotContain(leak, message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
