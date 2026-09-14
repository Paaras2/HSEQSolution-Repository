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

    public const string ValidFirstName = "کاربر";
    public const string ValidLastName = "آزمایشی";

    // پاسخِ checkCredential این دو را هم دارد؛ مقدار دارند تا بشود ثابت کرد به مرورگر نمی‌رسند.
    public const string ValidNationalCode = "0012345678";
    public const string ValidMobile = "09120000000";

    /// <summary>کاربری که هنوز با رمزِ پیش‌فرض (کد ملی) وارد می‌شود.</summary>
    public const string FirstLoginUsername = "3548";

    public const string FirstLoginPassword = "0098765432";

    /// <summary>کاربری که رمزش درست است (<see cref="ValidPassword"/>) ولی حسابش در UM غیرفعال است.</summary>
    public const string InactiveUsername = "7777";

    /// <summary>رمزی که سرویس UM را به خطای «سرویس در دسترس نیست» می‌اندازد.</summary>
    public const string PasswordThatBreaksUpstream = "trigger-upstream-outage";

    /// <summary>
    /// رمزی که پاسخِ غیر-JSON را شبیه‌سازی می‌کند - مثل صفحه‌ی HTMLِ یک پروکسی.
    /// UmService واقعی این را به ExternalAuthException تبدیل می‌کند، نه استثنای
    /// مدیریت‌نشده.
    /// </summary>
    public const string PasswordThatReturnsHtml = "trigger-html-response";

    /// <summary>
    /// پیامِ سرویس واقعیِ UM برای اعتبارنامه‌ی غلط، با همان کدِ ۴۱۰ - همان‌طور که از
    /// ‎https://usermanagement.odcc.ir/api/Auth/checkCredential‎ برگشت.
    /// </summary>
    public const string WrongCredentialsMessage = "اطلاعات وارد شده صحیح نمی باشد";

    public Task<CheckCredentialDto> CheckUserAndPassword(LoginRequestModel request)
    {
        if (request?.Password == PasswordThatBreaksUpstream)
            throw new ExternalAuthException("ارتباط با سرویس احراز هویت برقرار نشد. لطفاً چند لحظه بعد دوباره تلاش کنید.", 503);

        if (request?.Password == PasswordThatReturnsHtml)
            throw new ExternalAuthException("ارتباط با سرویس احراز هویت برقرار نشد. لطفاً چند لحظه بعد دوباره تلاش کنید.", 502);

        var username = request?.Username;
        var password = request?.Password;

        if (username == ValidUsername && password == ValidPassword)
            return Task.FromResult(Credential(3256, ValidFirstName, ValidLastName, isActive: true, isFirstLogin: false));

        if (username == FirstLoginUsername && password == FirstLoginPassword)
            return Task.FromResult(Credential(3548, "کاربر", "تازه‌وارد", isActive: true, isFirstLogin: true));

        if (username == InactiveUsername && password == ValidPassword)
            return Task.FromResult(Credential(7777, "کاربر", "غیرفعال", isActive: false, isFirstLogin: false));

        // همان شکلی که UmService واقعی از پاسخ ۴۰۰ سرویس UM می‌سازد.
        throw new ExternalAuthException(WrongCredentialsMessage, 410);
    }

    private static CheckCredentialDto Credential(int pcode, string firstName, string lastName, bool isActive, bool isFirstLogin) => new()
    {
        PCode = pcode,
        FirstName = firstName,
        LastName = lastName,
        Mobile = ValidMobile,
        NationalCode = ValidNationalCode,
        UserName = pcode.ToString(),
        IsActive = isActive,
        IsFirstLogin = isFirstLogin,
    };
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
