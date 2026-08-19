using System.IO;

namespace HSEQ.Service.Interfaces.Services
{
    // استخراج «بهترین‌تلاش» متن از فایل مدرک برای جستجوی پیشرفته در محتوا. هیچ پکیج
    // جدیدی برای پارس فایل نصب نشده - پیاده‌سازی فقط با کتابخانه‌های استاندارد دات‌نت
    // (System.IO.Compression / System.Xml) کار می‌کند، پس پشتیبانی از فرمت‌ها محدود است.
    public interface IFileTextExtractionService
    {
        // هرگز استثنا پرتاب نمی‌کند - شکست استخراج فقط یعنی نال برمی‌گردد، نه اینکه
        // بارگذاری مدرک با خطا متوقف شود. Stream را نمی‌بندد؛ فراخواننده مالک آن است.
        string? Extract(Stream stream, string fileName);
    }
}
