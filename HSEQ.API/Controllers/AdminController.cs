using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    // Authorize در سطح کلاس، همان الگوی بقیه‌ی کنترلرهای پروژه. قبلاً هیچ [Authorize]ی
    // اینجا نبود و مسیر Get برای همه باز بود؛ گذاشتنش روی کلاس یعنی اندپوینت بعدی هم
    // به‌طور پیش‌فرض بسته است، نه اینکه یادمان برود روی متد بگذاریم.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _isUserAdminService;
        private readonly IUserService _userService;
        private readonly IUnitOfWork _unitOfWork;
        // بازسازی متن فایل‌ها برای جستجو در محتوا.
        private readonly IDocumentTextIndexService _documentTextIndexService;

        // پنل ادمین برای هر دو نقش باز است. قواعد ریزتر (اینکه چه کسی چه نقشی می‌تواند
        // بدهد) در AdminService اعمال می‌شود، چون به وضعیت دیتابیس نیاز دارد.
        private const string AdminPanelRoles = "Admin,DocumentManager";

        public AdminController(
            IAdminService isUserAdminService,
            IUserService userService,
            IUnitOfWork unitOfWork,
            IDocumentTextIndexService documentTextIndexService)
        {
            _isUserAdminService = isUserAdminService;
            _userService = userService;
            _unitOfWork = unitOfWork;
            _documentTextIndexService = documentTextIndexService;
        }

        // «آیا این کد پرسنلی مدیر سیستم است؟» - اطلاعاتی درباره‌ی کاربرانِ دیگر است،
        // پس مثل بقیه‌ی مسیرهای این کنترلر فقط برای نقش‌های پنل ادمین باز است.
        [Authorize(Roles = AdminPanelRoles)]
        [HttpGet]
        [Route("Get")]
        public async Task<bool> Get(int pcode)
        {
            return await _isUserAdminService.IsAdminAsync(pcode);
        }

        // ---- مدیریت نقش کاربران ----

        [Authorize(Roles = AdminPanelRoles)]
        [HttpGet]
        [Route("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _isUserAdminService.GetUsersAsync();
            return Ok(users);
        }

        [Authorize(Roles = AdminPanelRoles)]
        [HttpPost]
        [Route("users/set-role")]
        public async Task<IActionResult> SetUserRole([FromForm] SetUserRoleRequestModel request)
        {
            var (actingPcode, actingRole) = GetActingUser();
            await _isUserAdminService.SetUserRoleAsync(request, actingPcode, actingRole);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = AdminPanelRoles)]
        [HttpPost]
        [Route("users/remove-role")]
        public async Task<IActionResult> RemoveUserRole([FromForm] int pcode)
        {
            var (actingPcode, actingRole) = GetActingUser();
            await _isUserAdminService.RemoveUserRoleAsync(pcode, actingPcode, actingRole);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        // بازسازی متن قابل‌جستجوی فایل اسناد.
        //
        // فقط Admin: کاری سنگین است که تمام فایل‌های آرشیو را می‌خواند و یک ستون کل
        // جدول اسناد را بازنویسی می‌کند - نه چیزی که مدیر مدارک در کار روزمره بزند.
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Route("reindex-file-text")]
        public async Task<DocumentTextIndexResult> ReindexFileText(
            [FromForm] bool onlyMissing,
            CancellationToken cancellationToken)
        {
            return await _documentTextIndexService.ReindexAsync(onlyMissing, cancellationToken);
        }

        // نقشِ درخواست‌دهنده از خود توکن خوانده می‌شود، نه از بدنه‌ی درخواست - وگرنه
        // کاربر می‌توانست ادعای نقش بالاتر کند و قواعد AdminService را دور بزند.
        private (int Pcode, AppRole Role) GetActingUser()
        {
            var pcode = Convert.ToInt32(_userService.GetPCodeFromToken(Request));
            var role = User.IsInRole("Admin") ? AppRole.Admin : AppRole.DocumentManager;
            return (pcode, role);
        }
    }
}
