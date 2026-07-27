using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Common
{
    public class AppSettingFactory
    {
        private static IConfiguration _configuration;
        public static AppSetting AppSetting { get; private set; }
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
            AppSetting = new AppSetting
            {
                SqlConnection = _configuration["ConnectionStrings:DefaultConnection"],
                JwtSettings = new JwtSettings
                {
                    Key = _configuration["Jwt:Key"],
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    ExpiryInMinutes = int.Parse(_configuration["Jwt:ExpiryInMinutes"])
                },
                //OTPSettings = new OTPSettings
                //{
                //    Domain = _configuration["OTP:Domain"],
                //    From = _configuration["OTP:From"],
                //    Password = _configuration["OTP:Password"],
                //    Username = _configuration["OTP:Username"],
                //    Url = _configuration["OTP:Url"],
                //    ApiPassword = _configuration["OTP:ApiPassword"],
                //    ApiUsername = _configuration["OTP:ApiUsername"]
                //},
                UploadPath = _configuration["UploadPath"],
                UMUrl = _configuration["UserManagementAPI:Url"],
            };
        }
    }

    public class AppSetting
    {
        public string SqlConnection { get; set; }
        public JwtSettings JwtSettings { get; set; }
        public OTPSettings OTPSettings { get; set; }
        public string UploadPath { get; set; }
        public string UMUrl { get; set; }
    }
    public class JwtSettings
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public int ExpiryInMinutes { get; set; }
    }
    public class OTPSettings
    {
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public string From { get; set; }
        public string ApiUsername { get; set; }
        public string ApiPassword { get; set; }
    }
}
