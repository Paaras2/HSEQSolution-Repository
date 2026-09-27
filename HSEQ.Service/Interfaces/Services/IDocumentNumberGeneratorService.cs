using HSEQ.Common;
using HSEQ.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    // Generates Document Numbers. A Number is immutable for the lifetime of a given
    // Document row through the ordinary create/revise/replace-file flows - a metadata
    // update never changes it, and issuing a revision produces a *new* row instead of
    // changing it. The one deliberate exception is ResolveEditedCodingBaseAsync /
    // BuildEditedCodingNumber below: an Admin/DocumentManager "fix the coding" escape
    // hatch that DOES rewrite Number on an existing row (see DocumentService.EditCodingAsync).
    public interface IDocumentNumberGeneratorService
    {
        // isEnglishVersion فقط پسوند " (EN)" را به انتهای شماره اضافه می‌کند؛ سریال و
        // بقیه‌ی اجزا دقیقاً مثل سند عادی گرفته می‌شوند.
        Task<GeneratedDocumentNumber> GenerateAsync(
            Guid projectId,
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId,
            bool isEnglishVersion);

        // Number for a new "ستاد" (Headquarters) document: O+AA+DD-SSS-R, no Project
        // prefix. SSS comes from the per-code counter (HeadquartersCodeCounter), not the
        // global Project sequence GenerateAsync above uses.
        Task<GeneratedDocumentNumber> GenerateHeadquartersAsync(
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId,
            bool isEnglishVersion);

        // Number for the next revision of an existing Document: same base code
        // (PPPP+O+AA+DD+SSS), advanced revision suffix. Deliberately does NOT touch the
        // serial sequence - a revision is the same document, so it keeps its serial.
        // پسوند " (EN)" هم از خودِ سند خوانده می‌شود: بازنگریِ یک نسخه‌ی انگلیسی باز هم
        // نسخه‌ی انگلیسی است.
        Task<GeneratedDocumentNumber> GenerateNextRevisionAsync(Document currentDocument);

        // برای ویرایشِ دستیِ کدگذاری (نه بازنگری، نه ایجاد): طبقه‌بندیِ تازه را
        // اعتبارسنجی و بخشِ پایه‌ی کد (کدها + سریال) را می‌سازد - یک‌بار برای کلِ
        // زنجیره‌ی یک سند، چون طبقه‌بندی/سریال در طولِ یک زنجیره یکسان است.
        // serialNumber=null یعنی سریالِ بعدیِ همان کد به‌صورت خودکار تخصیص یابد (دقیقاً
        // مثل GenerateAsync/GenerateHeadquartersAsync)؛ در غیر این صورت همان مقدار
        // عیناً استفاده می‌شود و هیچ شمارنده‌ای دست‌نمی‌خورد - انتخابِ دستیِ مدیر است.
        Task<EditedCodingBase> ResolveEditedCodingBaseAsync(
            DocumentCategory category,
            Guid? projectId,
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId,
            int? serialNumber);

        // شماره‌ی کاملِ یک ردیف را از رویِ پایه‌ی بالا و بازنگریِ همان ردیف می‌سازد.
        // یکتاییِ نتیجه را بررسی نمی‌کند - آن بررسی باید کلیدهای همینِ عملیات را نادیده
        // بگیرد، پس در DocumentService.EditCodingAsync انجام می‌شود، نه اینجا.
        string BuildEditedCodingNumber(
            EditedCodingBase codingBase,
            DocumentVersion lastVersion,
            int? contentRevision,
            bool isEnglishVersion);
    }
}