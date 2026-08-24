using HSEQ.Common;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // نوع اطلاعات پایه‌ای که عملیات روی آن انجام می‌شود. یک اندپوینت مشترک برای هر چهار
    // نوع، به‌جای چهار مجموعه اندپوینت تکراری.
    public enum MasterDataKind
    {
        Project = 0,
        DocumentType = 1,
        OrganizationalManagement = 2,
        OrganizationalActivity = 3
    }

    public class CreateMasterDataRequestModel
    {
        public MasterDataKind Kind { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }

        // فقط برای پروژه معنا دارد.
        public bool IsProjectRelated { get; set; }

        // فقط برای فعالیت سازمانی، و در آن حالت الزامی است.
        public Guid? OrganizationalManagementId { get; set; }
    }

    // کد عمداً اینجا نیست: کد داخل شماره‌ی مدارکِ صادرشده حک شده و تغییرش شماره‌ها را
    // با اطلاعات پایه ناهماهنگ می‌کند. فقط عنوان و وضعیت قابل ویرایش‌اند.
    public class UpdateMasterDataRequestModel
    {
        public MasterDataKind Kind { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }
        public bool IsProjectRelated { get; set; }
    }

    // فیلتر آرشیو شماره‌های قدیمی. ۷۶۸ ردیف است، پس برخلاف تب‌های اطلاعات پایه
    // صفحه‌بندی و جستجو سمت سرور انجام می‌شود نه در مرورگر.
    public class LegacyDocumentNumberQueryRequestModel
    {
        // جستجو در شماره خام، کد ۵حرفی، نام مدرک و واحد سازمانی.
        public string? Query { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SetUserRoleRequestModel
    {
        public int Pcode { get; set; }
        public AppRole Role { get; set; }
    }
}
