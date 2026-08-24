using HSEQ.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    // Generates Document Numbers. A Number is immutable for the lifetime of a given
    // Document row - a metadata update never changes it. Issuing a revision does not
    // change it either: it produces a *new* Document row carrying the next Number in
    // the sequence, leaving the superseded row untouched.
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
    }
}