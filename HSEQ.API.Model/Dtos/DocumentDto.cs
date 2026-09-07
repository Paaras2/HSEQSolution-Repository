using HSEQ.Common;
using System;
using System.Collections.Generic;

namespace HSEQ.API.Model.Dtos
{
    public class DocumentDto : BaseDto
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public string? FormerReviewShamsiDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }
        public string? CurrentReviewShamsiDate { get; set; }
        public DocumentVersion LastVersion { get; set; }
        public int? ContentRevision { get; set; }
        public int SerialNumber { get; set; }

        // نسخه‌ی انگلیسی؛ شماره‌اش با پسوند " (EN)" تمام می‌شود.
        public bool IsEnglishVersion { get; set; }

        // شماره‌اش با ساختار کدِ فعلی نمی‌خواند - مدرکی از سامانه‌ی قدیم که با شماره‌ی
        // تاریخیِ خودش وارد شده. کلاینت با همین یک مقدار نشانِ «خارج از کدینگ» را
        // روی ردیف می‌گذارد؛ هیچ رفتار دیگری به آن وابسته نیست.
        public bool IsOutsideCodingStructure { get; set; }
        // The revision this document supersedes, i.e. the previous link in the
        // revision chain. Null for a document's first revision.
        public Guid? RelatedDocumentId { get; set; }
        public string? RelatedDocumentNumber { get; set; }

        // شماره‌ی «مدارک مرتبط» این سند (جدول DocumentRelations) - با RelatedDocumentNumber
        // بالا اشتباه نشود که نسخه‌ی قبلی در زنجیره‌ی بازنگری است. فقط در فهرست صفحه‌بندی‌شده
        // (GetAllPaginationAsync) پر می‌شود؛ در بقیه‌ی مسیرها فهرست خالی است.
        public List<string> RelatedDocumentNumbers { get; set; } = new();

        // True when a newer revision of this document exists. Distinguishes a document
        // that went inactive because it was revised from one that was deactivated
        // outright - both have IsActive == false.
        public bool IsSuperseded { get; set; }

        public string FileName { get; set; }
        public DocumentCategory Category { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid OrganizationalManagementId { get; set; }
        public Guid OrganizationalActivityId { get; set; }
        public Guid DocumentTypeId { get; set; }
        public string? File { set; get; }
        public int CreatedByPCode { get; set; }

        // تکه‌ای از متن فایل که عبارت جستجو در آن پیدا شده. فقط وقتی پر می‌شود که
        // «جستجو در محتوای فایل» روشن باشد و تطابق در خودِ فایل رخ داده باشد - یعنی
        // کاربر می‌بیند چرا این سند در نتیجه آمده، نه اینکه حدس بزند.
        public string? ContentSnippet { get; set; }
    }
}