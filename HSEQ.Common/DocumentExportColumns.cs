using System.Collections.Generic;
using System.Linq;

namespace HSEQ.Common
{
    // فهرستِ کاملِ ستون‌های قابل‌انتخاب برای خروجی اکسلِ اسناد + کدام‌ها پیش‌فرض‌اند.
    // یک‌جا نگه داشته می‌شود تا سرویسِ ساختنِ فایل، اعتبارسنجیِ ورودی، و اندپوینتِ
    // فهرستِ ستون‌ها (برای منوی سفارشی‌سازیِ سمت کلاینت) همیشه با هم هماهنگ بمانند.
    public static class DocumentExportColumns
    {
        public const string Number = "number";
        public const string Name = "name";
        public const string Category = "category";
        public const string Status = "status";
        public const string Project = "project";
        public const string Management = "management";
        public const string Activity = "activity";
        public const string DocumentType = "documentType";
        public const string Revision = "revision";
        public const string FormerReviewDate = "formerReviewDate";
        public const string CurrentReviewDate = "currentReviewDate";
        public const string RelatedNumbers = "relatedNumbers";
        public const string FileName = "fileName";
        public const string CreatedBy = "createdBy";
        public const string CreatedTime = "createdTime";
        public const string ModifiedBy = "modifiedBy";
        public const string ModifiedDate = "modifiedDate";
        public const string IsOutsideCodingStructure = "isOutsideCodingStructure";
        public const string IsEnglishVersion = "isEnglishVersion";

        // ترتیب و برچسبِ نمایشیِ هر ستون - همان ترتیبی که هم در منوی انتخاب سمت
        // کلاینت و هم در خودِ فایل خروجی (وقتی انتخاب شود) دیده می‌شود.
        public static readonly (string Key, string Label)[] All =
        {
            (Number, "شماره"),
            (Name, "نام"),
            (Category, "دسته‌بندی"),
            (Status, "وضعیت"),
            (Project, "پروژه"),
            (Management, "مدیریت سازمانی"),
            (Activity, "فعالیت سازمانی"),
            (DocumentType, "نوع سند"),
            (Revision, "بازنگری"),
            (FormerReviewDate, "تاریخ بازبینی قبلی"),
            (CurrentReviewDate, "تاریخ بازبینی جاری"),
            (RelatedNumbers, "مدارک مرتبط"),
            (FileName, "نام فایل"),
            (CreatedBy, "ایجادکننده"),
            (CreatedTime, "تاریخ ایجاد"),
            (ModifiedBy, "آخرین ویرایش‌کننده"),
            (ModifiedDate, "تاریخ آخرین ویرایش"),
            (IsOutsideCodingStructure, "خارج از کدینگ"),
            (IsEnglishVersion, "نسخه انگلیسی"),
        };

        // مجموعه‌ی پیش‌فرض - همان ستون‌هایی که همیشه در خروجی بوده‌اند، تا رفتارِ
        // فعلی برای کسی که چیزی سفارشی نکرده عوض نشود.
        public static readonly string[] Default =
        {
            Number, Name, Category, Status, Project, Management, Activity,
            DocumentType, Revision, CurrentReviewDate, RelatedNumbers,
        };

        private static readonly HashSet<string> ValidKeys = All.Select(c => c.Key).ToHashSet();

        // ورودیِ کاربر پالایش می‌شود: کلیدهای نامعتبر (کهنه یا دستکاری‌شده) بی‌صدا
        // کنار می‌روند، نه اینکه کل خروجی را با خطا بترکانند. اگر پس از پالایش هیچ
        // ستونِ معتبری نماند، به پیش‌فرض برمی‌گردد.
        public static string[] Normalize(string[]? requested)
        {
            if (requested == null || requested.Length == 0)
                return Default;

            var filtered = requested.Where(ValidKeys.Contains).Distinct().ToArray();
            return filtered.Length > 0 ? filtered : Default;
        }
    }
}
