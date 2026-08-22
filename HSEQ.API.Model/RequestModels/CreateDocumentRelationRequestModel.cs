using HSEQ.Common;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // مبدأ همان مدرکی است که کاربر فهرست «مدارک مرتبط» آن را باز کرده، و مقصد مدرکی
    // است که از جستجو انتخاب کرده. جهت فقط برای نوع‌های جهت‌دار اهمیت دارد؛ در نمایش،
    // سمت مقابل برچسب معکوس می‌گیرد.
    public class CreateDocumentRelationRequestModel
    {
        public Guid SourceDocumentId { get; set; }
        public Guid TargetDocumentId { get; set; }
        public DocumentRelationType RelationType { get; set; }
        public string? Note { get; set; }
    }
}
