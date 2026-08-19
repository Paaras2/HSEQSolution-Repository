using HSEQ.Common;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // همه‌ی فیلترها اختیاری‌اند - هر کدام نال/خالی باشد نادیده گرفته می‌شود.
    public class SearchDocumentsRequestModel
    {
        // جستجو در شماره و نام سند (و در صورت SearchInFileContent، متن استخراج‌شده‌ی فایل هم).
        public string? Query { get; set; }

        // فیلتر وضعیت: نال=همه، true=فقط فعال، false=فقط غیرفعال.
        public bool? IsActive { get; set; }

        public DocumentCategory? Category { get; set; }
        public Guid? DocumentTypeId { get; set; }
        public Guid? OrganizationalManagementId { get; set; }
        public Guid? OrganizationalActivityId { get; set; }
        public Guid? ProjectId { get; set; }

        // فقط آخرین بازنگری هر زنجیره (سندی که هیچ بازنگری جدیدتری جایگزینش نکرده).
        public bool OnlyLatestRevision { get; set; }

        // اگر true باشد، Query در ExtractedText هم جستجو می‌شود، نه فقط شماره/نام.
        public bool SearchInFileContent { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
