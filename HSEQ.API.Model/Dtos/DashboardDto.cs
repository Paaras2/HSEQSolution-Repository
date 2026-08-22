using System;
using System.Collections.Generic;

namespace HSEQ.API.Model.Dtos
{
    // خروجی کامل داشبورد پیشرفته - همه‌ی محاسبات (KPI، هشدارها، نمودارها) سمت سرور
    // انجام می‌شود تا فرانت‌اند فقط رندر کند، نه اینکه کل جدول اسناد را بگیرد و خودش جمع بزند.
    public class DashboardSummaryDto
    {
        // --- کارت‌های KPI ---
        // تعداد «سندهای متمایز» یعنی فقط آخرین بازنگری هر زنجیره حساب می‌شود، نه هر ردیف
        // تاریخی؛ وگرنه TotalDocuments با هر بازنگری بی‌دلیل بزرگ‌تر می‌شد.
        public int TotalDocuments { get; set; }
        public int ActiveDocuments { get; set; }
        public int DueForReview { get; set; }
        public int OverdueReviews { get; set; }

        // --- هشدارها (هر کدام حداکثر ۱۰ مورد، به ترتیب فوریت) ---
        public List<DocumentAlertDto> ExpiredReviews { get; set; } = new();
        public List<DocumentAlertDto> DueWithin7Days { get; set; } = new();
        // بین روز ۸ تا ۳۰ - عمداً با DueWithin7Days همپوشانی ندارد تا یک سند دوبار نشان داده نشود.
        public List<DocumentAlertDto> DueWithin30Days { get; set; } = new();
        // نقطه‌ی کور سیستم: این اسناد چون تاریخ بازبینی ندارند، هرگز در سه هشدار بالا
        // ظاهر نمی‌شوند و بدون این کارت اصلاً دیده نمی‌شدند.
        public List<DocumentAlertDto> MissingReviewDate { get; set; } = new();

        // --- نمودارها ---
        public List<NamedCountDto> DocumentsByManagement { get; set; } = new();
        // فقط ۱۰ فعالیت پرتکرار - فعالیت‌ها نزدیک به ۶۰ مورد هستند و نمایش همه در یک
        // نمودار عملاً غیرقابل‌خواندن می‌شود.
        public List<NamedCountDto> DocumentsByActivity { get; set; } = new();
        public ReviewStatusBreakdownDto ReviewStatus { get; set; } = new();
        // ۱۲ ماه پیش‌رو، شامل ماه‌های بدون هیچ بازبینی (مقدار صفر) تا روند واقعاً پیوسته باشد.
        public List<MonthCountDto> ReviewTrend { get; set; } = new();
        // ۷ نوع سند پرتکرار + مابقی زیر «سایر» - نوع سند ۵۵ مقدار دارد، همان استدلال بالا.
        public List<NamedCountDto> DocumentsByType { get; set; } = new();
    }

    // یک ردیف عمومی «برچسب + تعداد» - برای نمودارهای مدیریت سازمانی، فعالیت، و نوع سند.
    public class NamedCountDto
    {
        public string Code { get; set; }
        public string Label { get; set; }
        public int Count { get; set; }
    }

    public class MonthCountDto
    {
        // فرمت yyyy-MM (میلادی) - هماهنگ با فرمت تاریخ‌های دیگر پروژه (مثلاً خروجی اکسل جستجو).
        public string MonthLabel { get; set; }
        public int Count { get; set; }
    }

    public class DocumentAlertDto
    {
        public Guid Key { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        // برای فهرست «بدون تاریخ بازبینی» هر دو نال‌اند - همان چیزی که آن هشدار را می‌سازد.
        public DateTime? ReviewDate { get; set; }
        // منفی یعنی از موعد گذشته (چند روز قبل)، مثبت یعنی چند روز تا موعد مانده.
        public int? DaysUntilDue { get; set; }
    }

    public class ReviewStatusBreakdownDto
    {
        public int OnTrack { get; set; }
        public int DueSoon { get; set; }
        public int Overdue { get; set; }
        public int NoDateSet { get; set; }
    }
}
