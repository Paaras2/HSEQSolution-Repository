using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HSEQ.API.ServiceConfiguration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// ردِ حسابرسی (ModifiedByPCode و ...) باید از هویتِ *تأییدشده‌ی* درخواست بیاید، نه از
/// چیزی که کاربر در بدنه می‌فرستد. این کلاس همان استخراج را می‌سنجد: فقط وقتی اصل
/// authenticated باشد، و فقط از claimِ درست.
/// </summary>
public class CurrentUserAccessorTests
{
    private static HttpContextCurrentUserAccessor AccessorFor(ClaimsPrincipal user)
    {
        var context = new DefaultHttpContext { User = user };
        return new HttpContextCurrentUserAccessor(new HttpContextAccessor { HttpContext = context });
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    [Fact]
    public void The_pcode_comes_from_the_sub_claim_of_an_authenticated_user()
    {
        var accessor = AccessorFor(Authenticated(new Claim(JwtRegisteredClaimNames.Sub, "3256")));

        Assert.Equal(3256, accessor.PCode);
    }

    [Fact]
    public void The_mapped_name_identifier_claim_is_also_accepted()
    {
        // هندلرِ JWT اغلب sub را به ClaimTypes.NameIdentifier نگاشت می‌کند.
        var accessor = AccessorFor(Authenticated(new Claim(ClaimTypes.NameIdentifier, "7777")));

        Assert.Equal(7777, accessor.PCode);
    }

    [Fact]
    public void An_unauthenticated_identity_yields_no_pcode()
    {
        // اصلی با claim ولی بدون authenticationType، احراز نشده حساب می‌شود.
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, "3256") }));

        Assert.Null(AccessorFor(anonymous).PCode);
    }

    [Fact]
    public void No_http_context_yields_no_pcode()
    {
        var accessor = new HttpContextCurrentUserAccessor(new HttpContextAccessor { HttpContext = null });

        Assert.Null(accessor.PCode);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void A_non_numeric_subject_is_ignored_rather_than_throwing(string subject)
    {
        var accessor = AccessorFor(Authenticated(new Claim(JwtRegisteredClaimNames.Sub, subject)));

        Assert.Null(accessor.PCode);
    }
}
