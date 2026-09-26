using HSEQ.Common;
using System;

namespace HSEQ.API.Model.Dtos
{
    // پاسخِ «رزرو کد»: کدِ ساخته‌شده و مهلتِ ضمیمه‌کردنِ فایل. کلاینت با Key بعداً فایل را
    // ضمیمه (نهایی) می‌کند و Number را به کاربر نشان می‌دهد.
    public class DocumentReservationDto
    {
        public Guid Key { get; set; }
        public string Number { get; set; }
        public DocumentStatus Status { get; set; }
        public DateTime? ReservationExpiresAtUtc { get; set; }
    }
}
