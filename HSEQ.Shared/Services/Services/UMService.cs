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
            catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx && socketEx.SocketErrorCode == SocketError.ConnectionRefused)
            {
                throw new ExternalAuthException("Cannot connect to authentication service. Please try again later.", 503);
            }
        }

        

        
    }
}
