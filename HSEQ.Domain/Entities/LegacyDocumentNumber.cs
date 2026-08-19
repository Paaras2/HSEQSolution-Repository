using HSEQ.Domain.Common;

namespace HSEQ.Domain.Entities
{
    // آرشیو شماره‌مدارک قدیمی سازمان (اسناد «ستاد»)، وارد شده از فایل اکسل ثبت مدارک شرکت.
    // این جدول فقط مرجع تاریخی است و مبنای ادامه‌ی شماره‌گذاری سریال به ازای هر کد ۵ حرفی
    // (HeadquartersCodeCounter) از همین داده‌ها ساخته می‌شود.
    public class LegacyDocumentNumber : BaseEntity
    {
        // متن اصلی شماره مدرک در اکسل (بعد از حذف علائم اعرابی و یادداشت‌های داخل پرانتز مثل "(EN)").
        public string RawNumber { get; set; }

        // کد ۵ حرفی (مدیریت+فعالیت+نوع سند) در صورتی که شماره با الگوی استاندارد مطابقت داشته باشد؛
        // برای چند ردیف استثنایی/ناقص اکسل (مثل "0000") نال است.
        public string? Code5 { get; set; }
        public int? SerialNumber { get; set; }

        // اگر خودِ شماره در اکسل یک پسوند بازنگری هم داشت (مثل "DGEPY-002-C")، همین‌جا ذخیره می‌شود.
        public string? RevisionSuffix { get; set; }

        public string? ManagementCode { get; set; }
        public string? ActivityCode { get; set; }
        public string? DocumentTypeCode { get; set; }

        public string? Name { get; set; }

        // ستون "Unit" اکسل - واحد سازمانی مالک مدرک در زمان تهیه فایل (ممکن است با کد مدیریت
        // فعلی یکی نباشد، چون واحدها می‌توانند از زمان صدور شماره‌ی قدیمی جابه‌جا/ادغام شده باشند).
        public string? UnitLabel { get; set; }

        // تاریخ‌های شمسی خام اکسل - عمداً به‌صورت متن نگه‌داری می‌شوند تا ریسک تبدیل اشتباه نداشته باشند.
        public string? LastEditShamsiDate { get; set; }
        public string? CurrentEditShamsiDate { get; set; }

        // ستون "Version" اکسل - آخرین حرف بازنگری مدرک در زمان تهیه فایل.
        public string? CurrentVersion { get; set; }
    }
}
