namespace HSEQ.Domain.Common
{
    public abstract class BaseEntity: IEntity, ITimeModification
    {
        public Guid Key { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // ردِ حسابرسی. کدِ پرسنلیِ کاربر است، نه نام؛ نگاشتش به نام از سامانه‌ی
        // مدیریت کاربران است. هر دو Nullable‌اند: داده‌ی پایه (seed) و ردیف‌های
        // قدیمی سازنده‌ی مشخصی ندارند. مقداردهی‌شان متمرکز در SaveChanges انجام
        // می‌شود (ApplicationDbContext) و از کلاینت خوانده نمی‌شود.
        public int? CreatedByPCode { get; set; }
        public int? ModifiedByPCode { get; set; }
    }



    //Define Marker Interface:
    public interface IEntity
    {

    }
    public interface ITimeModification
    {
        DateTime CreatedTime { get; set; }
        DateTime? ModifiedDate { get; set; }
    }
}
