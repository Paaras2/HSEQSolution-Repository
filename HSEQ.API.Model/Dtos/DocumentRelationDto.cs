using HSEQ.Common;
using System;

namespace HSEQ.API.Model.Dtos
{
    // یک ارتباط، از دید مدرکی که فهرست برای آن خواسته شده. همیشه مشخصات «سمت مقابل»
    // را برمی‌گرداند، نه خود مدرک جاری - پس کلاینت برای ساختن یک ردیف فهرست به هیچ
    // درخواست دیگری نیاز ندارد.
    public class DocumentRelationDto
    {
        // کلید ردیفِ ارتباط (نه کلید مدرک) - همان چیزی که برای حذف ارتباط لازم است.
        public Guid Key { get; set; }

        // مدرک سمت مقابل.
        public Guid DocumentId { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }

        // مدرک سمت مقابل بازنگری شده و نسخه‌ی جدیدتری دارد. برای هشدار دادن به کاربر
        // که ارتباط به یک نسخه‌ی از رده خارج اشاره می‌کند.
        public bool IsSuperseded { get; set; }

        public DocumentRelationType RelationType { get; set; }

        // true یعنی مدرک جاری، مبدأ این ارتباط است. برچسب نوع ارتباط برای نوع‌های
        // جهت‌دار (مرجع، پیوست، جایگزین) بر همین اساس معکوس می‌شود.
        public bool IsOutgoing { get; set; }

        public string? Note { get; set; }
        public DateTime CreatedTime { get; set; }
        public int CreatedByPCode { get; set; }
    }
}
