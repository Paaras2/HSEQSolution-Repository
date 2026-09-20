namespace HSEQ.Domain.Common
{
    /// <summary>
    /// کدِ پرسنلیِ کاربرِ درخواستِ جاری، از هویتِ *تأییدشده‌ی* توکن - نه از بدنه‌ی
    /// درخواست. مبنای ردِ حسابرسی در ApplicationDbContext است.
    ///
    /// در دامنه تعریف شده و پیاده‌سازی‌اش (روی HttpContext) در لایه‌ی API است، تا
    /// دامنه به ASP.NET Core وابسته نشود. بیرون از یک درخواست HTTP - seed هنگام
    /// راه‌اندازی، ابزار خط فرمان، آزمون‌ها - مقدارش null است و ردِ حسابرسی خالی می‌ماند.
    /// </summary>
    public interface ICurrentUserAccessor
    {
        int? PCode { get; }
    }
}
