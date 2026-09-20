using HSEQ.Domain.Common;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HSEQ.API.ServiceConfiguration
{
    /// <summary>
    /// کدِ پرسنلی را از هویتِ *تأییدشده‌ی* درخواست می‌خواند - همان اصلی که میان‌افزارِ
    /// احراز هویت پس از راستی‌آزماییِ امضا، صادرکننده، مخاطب و انقضای توکن ساخته است.
    /// عمداً از بدنه‌ی درخواست چیزی خوانده نمی‌شود؛ ردِ حسابرسی نباید از داده‌ای که
    /// خودِ کاربر می‌فرستد پر شود.
    /// </summary>
    public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
            => _httpContextAccessor = httpContextAccessor;

        public int? PCode
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated != true) return null;

                // JwtService کدِ پرسنلی را در claimِ sub می‌گذارد. هندلرِ JWT معمولاً sub
                // را به ClaimTypes.NameIdentifier نگاشت می‌کند، پس هر دو بررسی می‌شوند.
                var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pcode)
                    ? pcode
                    : null;
            }
        }
    }
}
