using HSEQ.Common;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // درخواستِ «رزرو کد»: فقط متادیتای لازم برای ساختِ شماره. نه فایل و نه تاریخ‌های
    // بازبینی - آن‌ها هنگام نهایی‌سازی/ویرایش بعدی داده می‌شوند. شماره هرگز از کلاینت
    // گرفته نمی‌شود؛ سرور آن را از همین اطلاعاتِ پایه می‌سازد (مثل CreateDocumentRequestModel).
    public class ReserveDocumentRequestModel
    {
        public string Name { get; set; }

        public bool IsEnglishVersion { get; set; }

        // پیش‌فرض «ستاد» (enum default = Headquarters).
        public DocumentCategory Category { get; set; } = DocumentCategory.Headquarters;

        // فقط برای Category == Project الزامی است؛ برای «ستاد» خالی می‌ماند.
        public Guid? ProjectId { get; set; }
        public Guid OrganizationalManagementId { get; set; }
        public Guid OrganizationalActivityId { get; set; }
        public Guid DocumentTypeId { get; set; }
    }
}
