using Microsoft.AspNetCore.Http;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // نهایی‌سازیِ یک رزرو: فایلِ مدرکِ رزروشده. کلید همان کلیدی است که پاسخِ Reserve
    // برگردانده. multipart/form-data ارسال می‌شود، مثل Add/Revise.
    public class AttachDocumentFileRequestModel
    {
        public Guid Key { get; set; }

        public IFormFile File { get; set; }
    }
}
