using HSEQ.Domain.Common;

namespace HSEQ.Domain.Entities
{
    // شمارنده‌ی سریال به ازای هر کد ۵ حرفی (مدیریت+فعالیت+نوع سند) اسناد «ستاد».
    // بر خلاف DocumentSerialSequence (که یک شمارنده‌ی سراسری برای اسناد پروژه است)، اینجا هر
    // کد ۵ حرفی شمارنده‌ی مستقل خودش را دارد - دقیقاً مطابق ساختار قدیمی اکسل (مثلاً QASFM
    // از 001 شروع می‌شود و مستقل از ASYFM پیش می‌رود). با آخرین سریال هر کد از آرشیو قدیمی
    // (LegacyDocumentNumber) مقداردهی اولیه می‌شود و از آن به بعد فقط از همین جدول ادامه پیدا می‌کند.
    public class HeadquartersCodeCounter : BaseEntity
    {
        public string Code5 { get; set; }
        public int LastSerialNumber { get; set; }
    }
}
