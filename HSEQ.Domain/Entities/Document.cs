using HSEQ.Common;
using HSEQ.Domain.Common;

namespace HSEQ.Domain.Entities
{
    public class Document : BaseEntity
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }

        // Major revision letter (A-Z). Reused as-is from the pre-existing DocumentVersion enum.
        public DocumentVersion LastVersion { get; set; }

        // Content revision (01-99). Only populated for Documents whose Project.IsProjectRelated == true.
        // Null for non-project documents, which use LastVersion alone (A, B, C, ...).
        public int? ContentRevision { get; set; }

        // Raw allocated value from the global SQL Server SEQUENCE (SSS component, 1-999).
        public int SerialNumber { get; set; }

        public Guid? RelatedDocumentId { get; set; }
        public string FileName { get; set; }

        // نسخه‌ی انگلیسی مدرک. بعضی مدارک هم نسخه‌ی فارسی دارند هم انگلیسی؛ در قرارداد
        // شرکت، نسخه‌ی انگلیسی با پسوند " (EN)" آخر شماره مشخص می‌شود. سریالش مستقل است،
        // یعنی مثل هر سند جدید دیگری از شمارنده گرفته می‌شود.
        public bool IsEnglishVersion { get; set; }

        // دسته‌بندی سند: ستاد (بدون پروژه) یا پروژه. ساختار شماره‌گذاری بر اساس همین تعیین می‌شود.
        public DocumentCategory Category { get; set; }

        // فقط برای اسناد دسته‌ی «پروژه» الزامی است؛ اسناد «ستاد» پروژه ندارند.
        public Guid? ProjectId { get; set; }
        public virtual Project Project { get; set; }

        public Guid OrganizationalManagementId { get; set; }
        public virtual OrganizationalManagement OrganizationalManagement { get; set; }

        public Guid OrganizationalActivityId { get; set; }
        public virtual OrganizationalActivity OrganizationalActivity { get; set; }

        public Guid DocumentTypeId { get; set; }
        public virtual DocumentType DocumentType { get; set; }

        public int CreatedByPCode { get; set; }

        // متن استخراج‌شده از فایل مدرک (در زمان بارگذاری/بازنگری) برای جستجوی پیشرفته در
        // محتوای فایل. برای نوع‌های پشتیبانی‌نشده (تصویر و...) یا در صورت شکست استخراج، نال است.
        public string? ExtractedText { get; set; }

        // مدرکی که شماره‌اش با ساختار کدِ فعلی نمی‌خواند - مدارکِ سامانه‌ی قدیم که کد
        // مدیریت یا فعالیت یا نوع سندشان دیگر در ساختار سازمانی تعریف نشده (مثل مدیریت
        // «K»)، یا اصلاً شماره‌شان کد نیست (مثل «CERTI-001» که یک کلمه است).
        //
        // این‌ها عمداً با شماره‌ی تاریخیِ خودشان وارد شده‌اند تا از دست نروند؛ همین پرچم
        // تنها چیزی است که آن‌ها را از مدارکِ استاندارد جدا می‌کند. برای مدرکی که از
        // داخل برنامه ساخته می‌شود همیشه false است، چون مولدِ شماره اجازه‌ی کدِ نامعتبر
        // نمی‌دهد. تصمیمِ نهایی درباره‌ی کدینگِ این مدارک بعداً گرفته می‌شود.
        public bool IsOutsideCodingStructure { get; set; }
    }
}
