using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Shared.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Sockets;
using System.Text;

namespace HSEQ.Shared.Services.Services
{
    public class UmService : IUMService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UmService> _logger;

        public UmService(HttpClient httpClient, ILogger<UmService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        // آنچه کاربر می‌بیند. عمداً هیچ جزئیاتی از داخل سرور ندارد - نه نشانی، نه
        // نوع خطا. تشخیص کارِ لاگ است، نه صفحه‌ی ورود.
        private const string UnavailableMessage =
            "ارتباط با سرویس احراز هویت برقرار نشد. لطفاً چند لحظه بعد دوباره تلاش کنید.";

        private void LogUnreachable(Exception ex, string what)
        {
            _logger.LogError(ex,
                "سرویس مدیریت کاربران در دسترس نیست - {What}. نشانی: {Url}",
                what, AppSettingFactory.AppSetting.UMUrl + "Auth/checkCredential");
        }

        /// <summary>
        /// «اتصال برقرار نشد» برای سه خرابیِ کاملاً متفاوت یکسان نوشته می‌شد، و هر سه
        /// روی ماشین‌های واقعیِ این استقرار دیده شده‌اند:
        ///
        ///   نام حل نمی‌شود       ماشینی که DNS داخلی را نمی‌شناسد - مثلاً عضو دامنه نیست.
        ///   اتصال برقرار نمی‌شود  مسیر یا فایروال؛ یک VPN تمام‌تونل روی همان ماشین هم
        ///                         ترافیکِ شبکه‌ی داخلی را به اینترنت می‌فرستد.
        ///   دست‌دهی TLS           گواهی با نامی که صدا زده شده جور نیست - مثلاً صدا زدن
        ///                         با IP، وقتی گواهی برای *.odcc.ir است.
        ///
        /// هر کدام کار دیگری لازم دارد، پس لاگ باید بگوید کدام است.
        /// </summary>
        private static string DescribeTransportFailure(HttpRequestException ex)
        {
            Uri.TryCreate(AppSettingFactory.AppSetting.UMUrl, UriKind.Absolute, out var uri);
            var host = uri?.Host ?? "?";
            var port = uri?.Port ?? 0;

            return ex.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError =>
                    $"نام «{host}» روی این ماشین حل نشد. DNS این ماشین آن را نمی‌شناسد؛ " +
                    "رکورد DNS داخلی را بدهید یا یک سطر در C:\\Windows\\System32\\drivers\\etc\\hosts بگذارید",

                HttpRequestError.ConnectionError =>
                    $"به {host}:{port} وصل نشد. مسیر شبکه یا فایروال است؛ اگر روی این ماشین VPN " +
                    "تمام‌تونل فعال است، ترافیک شبکه‌ی داخلی را هم به بیرون می‌فرستد",

                HttpRequestError.SecureConnectionError =>
                    $"دست‌دهی TLS با «{host}» شکست خورد. گواهی با این نام جور نیست یا ریشه‌اش برای " +
                    "این ماشین معتبر نیست؛ با نام میزبانِ روی گواهی صدا بزنید، نه با IP",

                _ => "اتصال برقرار نشد",
            };
        }

        private static T TryDeserialize<T>(string body) where T : class
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // فقط وقتی صدا زده می‌شود که بدنه JSON نبوده - یعنی یک صفحه‌ی خطا، نه داده‌ی
        // کاربر. پس بریده‌ای از خودِ متن لاگ می‌شود، چون همان چیزی است که می‌گوید چه
        // کسی واقعاً جواب داده: صفحه‌ی خطای IIS، هدایت پروکسی، یا چیز دیگر.
        private void LogUnexpectedBody(HttpResponseMessage response, string body, string url)
        {
            // بریده‌ای کوتاه از بدنه. خط‌های جدیدش دست‌نخورده می‌مانند - این یک
            // سطرِ لاگ است، نه یک شناسه، و خواندنِ چند سطری‌اش مشکلی ندارد.
            var preview = string.IsNullOrWhiteSpace(body)
                ? "<empty>"
                : body.Substring(0, Math.Min(200, body.Length));

            _logger.LogError(
                "پاسخ سرویس مدیریت کاربران JSON نبود. کد {Status}، نوع {ContentType}، طول {Length}، " +
                "WWW-Authenticate {Challenge}. نشانی: {Url}. آغاز بدنه: {Preview}",
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.ToString() ?? "<بدون نوع>",
                body?.Length ?? 0,
                response.Headers.WwwAuthenticate.Count > 0 ? response.Headers.WwwAuthenticate.ToString() : "<ندارد>",
                url,
                preview);
        }

        public async Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request)
        {
            try
            {
                var url = AppSettingFactory.AppSetting.UMUrl + "Auth/checkCredential";
                var response = await _httpClient.PostAsJsonAsync(url, request);

                // هدایت دنبال نمی‌شود (HttpClient این سرویس با AllowAutoRedirect=false
                // ساخته می‌شود) و اینجا به‌صورت خطای صریح گزارش می‌شود.
                //
                // دنبال کردنش دو ایراد داشت. یکی امنیتی: بدنه‌ی این درخواست رمز کاربر است،
                // و هدایتِ ۳۰۷/۳۰۸ آن را به هر مقصدی که پاسخ بگوید می‌فرستد. دیگری همان
                // چیزی که روی سرویس واقعی دیده شد: پورت ۸۰۳۰ به ‎https://<IP>‎ هدایت می‌کند
                // و گواهی برای نام دامنه است، پس ورود با خطای TLS شکست می‌خورد که هیچ ربطی
                // به علتش - نشانیِ اشتباه در تنظیمات - ندارد.
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    _logger.LogError(
                        "سرویس مدیریت کاربران به «{Location}» هدایت کرد (کد {Status}) و این برنامه دنبالش نمی‌رود. " +
                        "UserManagementAPI:Url را مستقیم روی نشانیِ نهایی بگذارید - معمولاً https و با نام میزبان. نشانی فعلی: {Url}",
                        response.Headers.Location?.ToString() ?? "<بدون Location>",
                        (int)response.StatusCode,
                        url);
                    throw new ExternalAuthException(UnavailableMessage, 503);
                }

                // بدنه یک‌بار به‌صورت متن خوانده می‌شود، نه مستقیم به JSON.
                //
                // ReadFromJsonAsync اگر بدنه JSON نباشد JsonException می‌دهد، و آن نه
                // HttpRequestException است نه TaskCanceledException - یعنی از هر دو
                // مهارِ پایین رد می‌شد و به‌صورت «Internal server error» و کد ۵۰۰ به
                // کاربر می‌رسید. سرویسی که پشت یک پروکسی نشسته باشد به‌راحتی یک صفحه‌ی
                // HTML برمی‌گرداند - صفحه‌ی خطا، هدایت به ورود، یا چالش احراز هویتِ
                // خودِ پروکسی - و آن‌وقت ورود با خطایی شکست می‌خورد که هیچ ربطی به
                // علتش ندارد.
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var credential = TryDeserialize<CheckCredentialDto>(body);
                    if (credential is null)
                    {
                        LogUnexpectedBody(response, body, url);
                        throw new ExternalAuthException(UnavailableMessage, 502);
                    }
                    return credential;
                }

                // پاسخِ خطا هم ممکن است JSON نباشد؛ آن‌وقت کد وضعیتِ خودِ HTTP
                // گویاترین چیزی است که داریم.
                var error = TryDeserialize<ErrorDto>(body);
                if (error is null)
                {
                    LogUnexpectedBody(response, body, url);
                    throw new ExternalAuthException(UnavailableMessage, (int)response.StatusCode);
                }

                throw new ExternalAuthException(error.Message ?? "خطایی رخ داده است",
                                                error.Code != 0 ? error.Code : (int)response.StatusCode);
            }
            catch (ExternalAuthException)
            {
                throw;
            }
            // Any transport-level failure reaching the User Management service - refused,
            // unreachable host, DNS failure, TLS failure, timeout - must surface as a
            // graceful "service unavailable" response, not an unhandled 500. Previously
            // only SocketError.ConnectionRefused was caught here; every other connectivity
            // failure (e.g. an unreachable internal host) fell through uncaught.
            catch (HttpRequestException ex)
            {
                // علت واقعی *فقط* در لاگ سرور می‌نشیند، نه در پاسخ.
                //
                // نشانیِ سرویس لاگ می‌شود، ولی هرگز خودِ request: آن شیء رمز کاربر را
                // دارد.
                LogUnreachable(ex, DescribeTransportFailure(ex));
                throw new ExternalAuthException(UnavailableMessage, 503);
            }
            catch (TaskCanceledException ex)
            {
                // مهلت تمام شد. این حالت با «اتصال رد شد» فرق دارد و معمولاً یعنی
                // بسته‌ها بی‌صدا دور ریخته می‌شوند - امضای یک قاعده‌ی فایروال، یا VPN
                // تمام‌تونلی روی همین ماشین که ترافیک داخلی را به بیرون می‌فرستد.
                LogUnreachable(ex, $"مهلت {_httpClient.Timeout.TotalSeconds:0} ثانیه‌ای تمام شد (بسته‌ها احتمالاً دور ریخته می‌شوند - فایروال، یا VPN تمام‌تونل روی همین ماشین)");
                throw new ExternalAuthException(UnavailableMessage, 503);
            }
        }
    }
}
