using HSEQ.Common;
using HSEQ.Domain.Common;

namespace HSEQ.Domain.Entities
{
    // ارتباط میان دو مدرکِ مستقل - مثلاً یک دستورالعمل و فرمی که با آن پر می‌شود، یا یک
    // روش اجرایی و خط‌مشیِ بالادستی‌اش.
    //
    // چرا جدول جداگانه و نه یک فیلد روی Document:
    //   ۱) Document.RelatedDocumentId از قبل معنای دیگری دارد (نسخه‌ی قبلی در زنجیره‌ی
    //      بازنگری) و نمی‌توان آن را دوباره بار کرد.
    //   ۲) هر مدرک می‌تواند به چند مدرک مرتبط باشد، پس رابطه چند-به-چند است.
    //
    // ردیف جهت‌دار ذخیره می‌شود (مبدأ ← مقصد) چون بعضی نوع‌ها جهت دارند، اما خواندن از
    // هر دو سمت انجام می‌شود: فهرست «مدارک مرتبط» یک مدرک، هم ردیف‌هایی که در آن‌ها مبدأ
    // است و هم ردیف‌هایی که در آن‌ها مقصد است را شامل می‌شود.
    public class DocumentRelation : BaseEntity
    {
        public Guid SourceDocumentId { get; set; }
        public virtual Document SourceDocument { get; set; }

        public Guid TargetDocumentId { get; set; }
        public virtual Document TargetDocument { get; set; }

        public DocumentRelationType RelationType { get; set; }

        // توضیح اختیاری کاربر درباره‌ی اینکه این دو مدرک چرا به هم مرتبط‌اند.
        public string? Note { get; set; }

        public int CreatedByPCode { get; set; }
    }
}
