using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IUserService
    {
        Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request);
        string GetPCodeFromToken(HttpRequest request);
    }
}
