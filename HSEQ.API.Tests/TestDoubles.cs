using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using HSEQ.Shared.Interfaces.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HSEQ.API.Tests;

/// <summary>
/// جای سرویس بیرونی User Management. تنها وابستگی شبکه‌ایِ مسیر ورود همین است، پس
/// با جایگزینی‌اش کل جریان ورود - کنترلر، ساخت توکن، احراز هویت - بدون شبکه آزمودنی
/// می‌شود. بقیه‌ی زنجیره واقعی می‌ماند.
/// </summary>
public sealed class StubUmService : IUMService
{
    /// <summary>کد پرسنلی‌ای که «معتبر» شناخته می‌شود.</summary>
    public const string ValidUsername = "3256";

    public const string ValidPassword = "correct-horse";

    /// <summary>رمزی که سرویس UM را به خطای «سرویس در دسترس نیست» می‌اندازد.</summary>
    public const string PasswordThatBreaksUpstream = "trigger-upstream-outage";

    public Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request)
    {
        if (request?.Password == PasswordThatBreaksUpstream)
            throw new ExternalAuthException("Cannot connect to authentication service. Please try again later.", 503);

        if (request?.Username != ValidUsername || request.Password != ValidPassword)
        {
            // همان شکلی که UmService واقعی از پاسخ غیر ۲xx می‌سازد.
            throw new ExternalAuthException("نام کاربری یا رمز عبور نامعتبر است", 401);
        }

        return Task.FromResult(new CheckCredentialDto
        {
            PCode = int.Parse(ValidUsername),
            IsActive = true,
        });
    }
}

/// <summary>
/// نقشِ کاربر از جدول Admins خوانده می‌شود؛ اینجا جایگزین می‌شود تا آزمون‌های
/// مسیریابی و ورود به دیتابیس گره نخورند.
/// </summary>
public sealed class StubAdminService : IAdminService
{
    public Task<bool> IsAdminAsync(int pcode) => Task.FromResult(true);

    public Task<AppRole?> GetRoleAsync(int pcode) => Task.FromResult<AppRole?>(AppRole.Admin);

    public Task<List<AppUserDto>> GetUsersAsync() => Task.FromResult(new List<AppUserDto>());

    public Task SetUserRoleAsync(SetUserRoleRequestModel request, int actingPcode, AppRole actingRole)
        => Task.CompletedTask;

    public Task RemoveUserRoleAsync(int pcode, int actingPcode, AppRole actingRole) => Task.CompletedTask;
}

/// <summary>
/// آنچه اکشن در لحظه‌ی اجرا از درخواست می‌بیند.
///
/// چرا لازم است: «۴۰۴ نگرفتن» فقط می‌گوید مسیر جور در آمده. این‌که ‎PathBase‎ هم
/// دست‌نخورده مانده جداگانه اهمیت دارد، چون هر نشانی‌ای که برنامه تولید می‌کند
/// (ریدایرکت، هدر Location، نشانی سرورِ Swagger) از روی همان ساخته می‌شود.
/// </summary>
public sealed class RequestPathRecorder
{
    public string? PathBase { get; set; }
    public string? Path { get; set; }
}

public sealed class RequestPathRecordingFilter : IActionFilter
{
    private readonly RequestPathRecorder _recorder;

    public RequestPathRecordingFilter(RequestPathRecorder recorder) => _recorder = recorder;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        _recorder.PathBase = context.HttpContext.Request.PathBase.Value;
        _recorder.Path = context.HttpContext.Request.Path.Value;
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
