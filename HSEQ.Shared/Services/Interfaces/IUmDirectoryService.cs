using System.Collections.Generic;
using System.Threading.Tasks;

namespace HSEQ.Shared.Interfaces.Services
{
    /// <summary>
    /// نگاشتِ کد پرسنلی به نام کامل، برای ردِ حسابرسی (چه کسی سند را ایجاد/بازنگری
    /// کرده). عمداً از IUMService جداست: آن یکی احراز هویت را انجام می‌دهد و پشتش
    /// می‌تواند سرویسِ HTTP باشد یا دیتابیس (UserManagement:Source)، اما نمایشِ نام
    /// یک نیازِ فقط-خواندنی است و همیشه مستقیم از دیتابیس UM می‌آید - چه احراز هویت
    /// از کدام مسیر برود.
    /// </summary>
    public interface IUmDirectoryService
    {
        // کدهای پیدانشده در نتیجه غایب‌اند، نه با مقدار خالی - فراخوان با
        // TryGetValue تشخیص می‌دهد و به‌جایش کد پرسنلی را نشان می‌دهد.
        Task<Dictionary<int, string>> GetFullNamesAsync(IEnumerable<int> pCodes);
    }
}
