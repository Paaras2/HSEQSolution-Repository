using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Shared.Interfaces.Services;
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
        public UmService(HttpClient httpClient)
        {
            _httpClient = httpClient;
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
            catch (HttpRequestException)
            {
                throw new ExternalAuthException("Cannot connect to authentication service. Please try again later.", 503);
            }
            catch (TaskCanceledException)
            {
                throw new ExternalAuthException("Cannot connect to authentication service. Please try again later.", 503);
            }
        }
    }
}
