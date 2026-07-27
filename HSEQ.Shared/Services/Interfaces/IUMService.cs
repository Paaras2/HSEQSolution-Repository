using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.Shared.Interfaces.Services
{
    public interface IUMService
    {
        Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request);
    }
}
