using HSEQ.Common;
using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace HSEQ.Domain.Entities
{
    // یک کاربرِ دارای نقش. نام جدول به‌خاطر سازگاری با داده‌ی موجود «Admins» مانده،
    // اما از این پس هم «مدیر سیستم» و هم «مدیر اسناد» را نگه می‌دارد.
    public class Admin : BaseEntity
    {
        public int Pcode { get; set; }

        // ستون جدید. مهاجرت برای ردیف‌های موجود مقدار Admin می‌گذارد تا دسترسی کسی کم نشود.
        public AppRole Role { get; set; }
    }
}
