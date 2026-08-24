using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IAdminService
    {
        Task<bool> IsAdminAsync(int Pcode);

        // نقش ذخیره‌شده‌ی کاربر، یا null اگر ردیفی نداشته باشد (یعنی «فقط مشاهده»).
        Task<AppRole?> GetRoleAsync(int pcode);

        // فهرست کاربرانِ دارای نقش، برای پنل ادمین.
        Task<List<AppUserDto>> GetUsersAsync();

        // ثبت یا تغییر نقشِ یک کاربر. actingPcode/actingRole برای اعمال قواعد دسترسی
        // لازم‌اند: کسی نمی‌تواند نقش خودش را عوض کند و فقط مدیر سیستم می‌تواند
        // مدیر سیستم بسازد یا حذف کند.
        Task SetUserRoleAsync(SetUserRoleRequestModel request, int actingPcode, AppRole actingRole);

        // برداشتن نقش - کاربر به «فقط مشاهده» برمی‌گردد.
        Task RemoveUserRoleAsync(int pcode, int actingPcode, AppRole actingRole);
    }
}
