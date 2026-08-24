using HSEQ.Common;
using System;

namespace HSEQ.API.Model.Dtos
{
    // یک کاربرِ دارای نقش، در فهرست پنل ادمین. کاربران بدون ردیف اینجا نمی‌آیند -
    // نبودِ ردیف یعنی «فقط مشاهده».
    public class AppUserDto
    {
        public Guid Key { get; set; }
        public int Pcode { get; set; }
        public AppRole Role { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    // یک ردیف از آرشیو شماره‌مدارک قدیمی ستاد. فقط خواندنی است: این جدول یک‌بار از
    // اکسل ثبت مدارک شرکت وارد شده و هیچ‌وقت به‌روزرسانی یا حذف نمی‌شود.
    public class LegacyDocumentNumberDto
    {
        public Guid Key { get; set; }
        public string RawNumber { get; set; }
        public string? Code5 { get; set; }
        public int? SerialNumber { get; set; }
        public string? RevisionSuffix { get; set; }
        public string? ManagementCode { get; set; }
        public string? ActivityCode { get; set; }
        public string? DocumentTypeCode { get; set; }
        public string? Name { get; set; }
        public string? UnitLabel { get; set; }

        // تاریخ‌ها عمداً متن خام شمسی‌اند - همان‌طور که در جدول ذخیره شده‌اند.
        public string? LastEditShamsiDate { get; set; }
        public string? CurrentEditShamsiDate { get; set; }
        public string? CurrentVersion { get; set; }
    }

    // صفحه‌بندی مخصوص آرشیو. PagedResult موجود به DocumentDto گره خورده است و
    // جنریک‌کردنش کل مسیر اسناد و جستجو را درگیر می‌کرد.
    public class LegacyDocumentNumberPagedResult
    {
        public List<LegacyDocumentNumberDto> Items { get; set; } = [];
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // یک ردیف اطلاعات پایه در پنل ادمین. هر چهار نوع (پروژه، نوع سند، مدیریت و فعالیت
    // سازمانی) همین شکل را دارند؛ فیلدهای اختیاری فقط برای نوعی که به آن‌ها نیاز دارد
    // پر می‌شوند تا کلاینت یک جدول مشترک داشته باشد نه چهار تا.
    public class MasterDataItemDto
    {
        public Guid Key { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }

        // فقط پروژه
        public bool? IsProjectRelated { get; set; }

        // فقط فعالیت سازمانی
        public Guid? OrganizationalManagementId { get; set; }

        // تعداد مدارکی که به این ردیف وابسته‌اند. مبنای «آیا کد هنوز قابل تغییر است؟»
        // و هشدار پیش از غیرفعال‌سازی.
        public int UsageCount { get; set; }
    }
}
