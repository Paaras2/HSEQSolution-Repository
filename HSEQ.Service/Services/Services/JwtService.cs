using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Services
{
    public class JwtService : IJwtService
    {
        private readonly string _secretKey;
        private readonly string _audience;
        private readonly string _issuer;
        private readonly int _expiresMin;
        private readonly IAdminService _adminService;
        public JwtService(IAdminService adminService)
        {
            _secretKey = AppSettingFactory.AppSetting.JwtSettings.Key;
            _issuer = AppSettingFactory.AppSetting.JwtSettings.Issuer;
            _audience = AppSettingFactory.AppSetting.JwtSettings.Audience;
            _expiresMin = AppSettingFactory.AppSetting.JwtSettings.ExpiryInMinutes;
            _adminService = adminService;
        }

        public async Task<string> GenerateJwtToken(string username, string givenName = null, string familyName = null)
        {
            // نقش از جدول Admins خوانده می‌شود. نبودِ ردیف یعنی کاربر نقشی ندارد و
            // کلاینت او را «فقط مشاهده» می‌بیند («Addi» همان مقدار قبلی است تا رفتار
            // توکن‌های موجود عوض نشود).
            var storedRole = await _adminService.GetRoleAsync(Convert.ToInt32(username));
            string role = storedRole switch
            {
                AppRole.Admin => "Admin",
                AppRole.DocumentManager => "DocumentManager",
                _ => "Addi"
            };
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, username.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
            };
            AddNameClaims(claims, givenName, familyName);

            return WriteToken(claims);
        }

        // Dev-only shortcut used by AuthController.DevLogin to mint a token with an
        // explicit role claim, bypassing the UM service and the Admins table lookup
        // entirely - so it keeps working locally even when those are unreachable.
        public Task<string> GenerateDevToken(string username, string role, string givenName = null, string familyName = null)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
            };
            AddNameClaims(claims, givenName, familyName);

            return Task.FromResult(WriteToken(claims));
        }

        // نام و نام خانوادگی از پاسخِ checkCredential سامانه‌ی مدیریت کاربران می‌آید و در
        // توکن می‌نشیند تا رابط کاربری نامِ کاربر را نشان دهد - پس از ری‌لود صفحه هم،
        // بدون ذخیره‌ی جداگانه‌ای که بتواند با توکن ناهمخوان شود. فقط برای نمایش است؛
        // هیچ تصمیمِ دسترسی‌ای روی آن گرفته نمی‌شود.
        private static void AddNameClaims(List<Claim> claims, string givenName, string familyName)
        {
            if (!string.IsNullOrWhiteSpace(givenName))
                claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, givenName.Trim()));
            if (!string.IsNullOrWhiteSpace(familyName))
                claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, familyName.Trim()));
        }

        private string WriteToken(IEnumerable<Claim> claims)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expiresMin),
                signingCredentials: credentials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
