using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Shared.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
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

        public async Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(AppSettingFactory.AppSetting.UMUrl + "Auth/checkCredential", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CheckCredentialDto>();
                }
                else
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
                    throw new ExternalAuthException(error?.Message ?? "خطایی رخ داده است", error?.Code ?? 400);
                }
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
                // پیش از این، استثنا گرفته و دور ریخته می‌شد: اپراتور «Cannot connect»
                // می‌دید و هیچ راهی نداشت بفهمد اتصال رد شده، نام حل نشده، یا فایروال
                // بسته‌ها را دور ریخته - در حالی که خودِ برنامه دقیقاً می‌دانست.
                // نتیجه‌اش کاوشِ دستی شبکه بود برای چیزی که همان لحظه معلوم بود.
                //
                // نشانیِ سرویس لاگ می‌شود، ولی هرگز خودِ request: آن شیء رمز کاربر را
                // دارد.
                LogUnreachable(ex, "اتصال برقرار نشد");
                throw new ExternalAuthException(UnavailableMessage, 503);
            }
            catch (TaskCanceledException ex)
            {
                // مهلت تمام شد. این حالت با «اتصال رد شد» فرق دارد و معمولاً یعنی
                // بسته‌ها بی‌صدا دور ریخته می‌شوند - امضای یک قاعده‌ی فایروال، نه
                // سرویسی که بالا نیست.
                LogUnreachable(ex, $"مهلت {_httpClient.Timeout.TotalSeconds:0} ثانیه‌ای تمام شد (بسته‌ها احتمالاً دور ریخته می‌شوند - فایروال)");
                throw new ExternalAuthException(UnavailableMessage, 503);
            }
        }
    }
}
