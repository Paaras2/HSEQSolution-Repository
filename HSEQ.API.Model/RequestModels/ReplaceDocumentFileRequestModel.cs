using Microsoft.AspNetCore.Http;
using System;

namespace HSEQ.API.Model.RequestModels
{
    // جایگزینیِ فایلِ یک سند بدون بازنگری: برخلاف ReviseDocumentRequestModel هیچ سطرِ
    // تازه‌ای ساخته نمی‌شود و شماره/حرفِ بازنگری دست‌نخورده می‌ماند - فقط فایلِ همین سند
    // عوض می‌شود. برای اصلاحاتی که به بازنگریِ رسمی نیاز ندارند (مثل رفعِ یک خطای تایپی
    // یا فرمت)، نه برای تغییرِ محتوایی سند - see IDocumentService.ReplaceFileAsync.
    public class ReplaceDocumentFileRequestModel
    {
        public Guid Key { get; set; }

        // Optional: می‌تواند نام را هم همراه با جایگزینیِ فایل به‌روزرسانی کند.
        public string? Name { get; set; }

        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }

        // Required in practice, نال‌پذیر تا نبودش با پیامِ اختصاصی رد شود، نه با
        // ProblemDetails خامِ [ApiController].
        public IFormFile? File { get; set; }
    }
}
