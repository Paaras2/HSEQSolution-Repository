using HSEQ.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.API.Controllers
{
    // بررسی سلامت برای اسکریپت استقرار و پایش سرور.
    //
    // چرا لازم شد: پیش از این، بررسی سلامتِ استقرار یک مسیر محافظت‌شده را صدا می‌زد و
    // پاسخ 401 را «سالم» حساب می‌کرد. آن فقط ثابت می‌کند لوله‌ی وب بالا آمده - اگر رشته‌ی
    // اتصال غلط بود، استقرار «موفق» گزارش می‌شد و خرابی تازه موقع کار کاربر پیدا می‌شد.
    //
    // عمداً بدون احراز هویت است: اسکریپت استقرار توکنی ندارد و پایشِ سرور هم نباید
    // حساب کاربری بخواهد.
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public HealthController(ApplicationDbContext context)
        {
            _context = context;
        }

        // خروجی عمداً حداقلی است: فقط سالم/ناسالم. نه نسخه، نه نام میزبان، نه متن خطا -
        // این مسیر بدون احراز هویت باز است و نباید چیزی درباره‌ی داخلِ سرور بگوید.
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            bool databaseReachable;
            try
            {
                databaseReachable = await _context.Database.CanConnectAsync(cancellationToken);
            }
            catch
            {
                // هر خطایی در اتصال یعنی ناسالم. جزئیاتش عمداً بیرون نمی‌رود.
                databaseReachable = false;
            }

            if (!databaseReachable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy" });

            return Ok(new { status = "Healthy" });
        }
    }
}
