using HSEQ.Common;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // ویرایشِ کدگذاریِ یک سندِ موجود - طبقه‌بندی (مدیریت/فعالیت/نوع سند) + سریال (SSS)
    // و/یا بازنگری (R) را با صلاحدیدِ مدیر اسناد/سیستم مستقیماً تغییر می‌دهد، برخلافِ
    // بقیه‌ی مسیرها که این‌ها را فقط سرور و فقط طبق قاعده تولید می‌کند. عملیاتی
    // استثنایی برای اصلاحِ یک کدگذاریِ اشتباه، نه بخشی از گردشِ کارِ عادیِ سند - see
    // IDocumentService.EditCodingAsync.
    //
    // دسته‌بندی (ستاد/پروژه) و پروژه از این مسیر قابل تغییر نیستند: تغییرِ آن‌ها یعنی
    // عوض‌شدنِ کلِ ساختارِ شماره (پیشوندِ پروژه، طرحِ بازنگری)، نه یک اصلاحِ کدگذاری.
    public class EditDocumentCodingRequestModel
    {
        public Guid Key { get; set; }

        // مدیریت/فعالیت/نوع سند و سریال یک گروه‌اند: هر بازنگریِ زنجیره‌ی این سند
        // طبقه‌بندی/سریالِ یکسان دارد، پس این تغییر روی کل زنجیره اعمال می‌شود، نه فقط
        // روی Key. false یعنی این گروه دست‌نخورده می‌ماند و بقیه‌ی فیلدهای زیر نادیده گرفته می‌شوند.
        public bool ChangeClassification { get; set; }
        public Guid OrganizationalManagementId { get; set; }
        public Guid OrganizationalActivityId { get; set; }
        public Guid DocumentTypeId { get; set; }

        // نال یعنی سریالِ بعدیِ کدِ تازه به‌صورت خودکار تخصیص یابد.
        public int? SerialNumber { get; set; }

        // برخلاف بالا، فقط روی همینِ یک ردیف (Key) اثر می‌گذارد - بقیه‌ی زنجیره حرفِ
        // بازنگریِ خودش را حفظ می‌کند. false یعنی این گروه دست‌نخورده می‌ماند.
        public bool ChangeRevision { get; set; }
        public DocumentVersion NewMajorVersion { get; set; }
        public int? NewContentRevision { get; set; }
    }
}
