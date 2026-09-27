using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IDocumentService
    {
        // کلید سند تازه‌ساخته‌شده را برمی‌گرداند تا فراخوان بتواند بلافاصله - و در همان
        // رفت‌وبرگشت کاربر - کارهای وابسته به آن سند را انجام دهد، مثل ثبت مدارک مرتبطی
        // که در فرم افزودن انتخاب شده‌اند. کلیدهای Guid را EF سمت کلاینت هنگام Add تولید
        // می‌کند، پس مقدار پیش از SaveChanges هم معتبر است.
        Task<Guid> AddAsync(CreateDocumentRequestModel request, string pcode);
        Task UpdateAsync(UpdateDocumentRequestModel request);

        // Issues the next revision of an existing Document as a NEW Document row and
        // retires the one it supersedes. Never mutates the superseded revision's
        // Number, revision components or file - that history has to stay readable.
        Task ReviseAsync(ReviseDocumentRequestModel request, string pcode);

        // جایگزینیِ فایلِ همین سند، بدون بازنگری: برخلاف ReviseAsync هیچ سطرِ تازه‌ای
        // نمی‌سازد و شماره/حرفِ بازنگری همان می‌ماند - فقط محتوای پیوست عوض می‌شود.
        Task ReplaceFileAsync(ReplaceDocumentFileRequestModel request);

        // ویرایشِ کدگذاریِ یک سندِ موجود با صلاحدیدِ مدیر: مدیریت/فعالیت/نوع‌سند/سریال
        // (اگر ChangeClassification) روی کلِ زنجیره‌ی بازنگریِ این سند اعمال می‌شود -
        // چون GetRevisionHistoryAsync زنجیره را دقیقاً از رویِ همین فیلدها پیدا می‌کند -
        // و حرفِ بازنگری/شماره‌ی فرعی (اگر ChangeRevision) فقط روی همین یک ردیف. شماره‌ی
        // هر ردیفِ اثرگرفته از نو ساخته و یکتاییِ همه با هم، پیش از نوشتنِ حتی یکی،
        // بررسی می‌شود - عملیات همه‌یا-هیچ است.
        Task EditCodingAsync(EditDocumentCodingRequestModel request);

        Task DeleteAsync(Guid documenttId);
        Task<List<DocumentDto>> GetAllAsync(bool includeDeactiveItems = true);
        Task<DocumentDto> GetByIdAsync(Guid id);

        // Opens the stored file of one specific document. Works for superseded
        // revisions too - each revision keeps its own file, so history stays readable.
        Task<DocumentFileResult> GetFileAsync(Guid documentId);

        // Every revision of the document `documentId` belongs to, oldest first,
        // regardless of which revision in the chain was asked for.
        Task<List<DocumentDto>> GetRevisionHistoryAsync(Guid documentId);
        Task<PagedResult> GetAllPaginationAsync(
            int pageNumber,
            int pageSize,
            bool includeDeactiveItems = true);
    }
}
