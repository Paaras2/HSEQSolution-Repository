using HSEQ.Common;
using System;

namespace HSEQ.Service.Interfaces.Services
{
    // بخشِ «پایه»ی کد برای ویرایشِ کدگذاریِ یک سندِ موجود - همان چیزی که در طولِ کلِ
    // زنجیره‌ی بازنگریِ یک سند یکسان است (طبقه‌بندی + سریال)، بدون بازنگری. یک‌بار در
    // IDocumentNumberGeneratorService.ResolveEditedCodingBaseAsync ساخته می‌شود و بعد
    // برای هر ردیفِ زنجیره (با حرفِ بازنگریِ خودش) به BuildEditedCodingNumber داده می‌شود.
    public sealed class EditedCodingBase
    {
        public DocumentCategory Category { get; }
        public Guid? ProjectId { get; }
        public string ProjectCode { get; }

        // از Project.IsProjectRelated (پروژه) یا همیشه false (ستاد) - تعیین می‌کند که
        // RevisionCode شماره‌ی فرعی (ContentRevision) بپذیرد یا نه.
        public bool IsProjectRelated { get; }

        public string ManagementCode { get; }
        public string ActivityCode { get; }
        public string DocumentTypeCode { get; }
        public int SerialNumber { get; }

        public EditedCodingBase(
            DocumentCategory category,
            Guid? projectId,
            string projectCode,
            bool isProjectRelated,
            string managementCode,
            string activityCode,
            string documentTypeCode,
            int serialNumber)
        {
            Category = category;
            ProjectId = projectId;
            ProjectCode = projectCode;
            IsProjectRelated = isProjectRelated;
            ManagementCode = managementCode;
            ActivityCode = activityCode;
            DocumentTypeCode = documentTypeCode;
            SerialNumber = serialNumber;
        }
    }
}
