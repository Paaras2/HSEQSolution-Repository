using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Service.Interfaces.Services;
using HSEQ.Shared.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace HSEQ.Service.Services.Services
{
    public class UserService : IUserService
    {
        private readonly IUMService _umService;
        public UserService(IUMService umService)
        {
            _umService = umService;
        }
        public async Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request)
        {
            return await _umService.CheckUserAndPassword(request);
        }

        public string GetPCodeFromToken(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"].ToString();

            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(authHeader))
                throw new Exception("هدر اتوریزیشن نمی تواند خالی باشد");

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var claims = jwtToken.Claims.Select(c => new
            {
                Type = c.Type,
                Value = c.Value
            }).ToList();
            var userDomain = claims.FirstOrDefault(r => r.Type == "sub").Value;
            return userDomain;
        }
    }
}
