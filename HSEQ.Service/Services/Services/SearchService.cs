using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // ارزش واقعی این سرویس در سه چیز است: پیدا کردن دقیق مدرک (فیلترهای ترکیبی + جستجوی
    // متن)، تشخیص آخرین نسخه‌ی معتبر (OnlyLatestRevision)، و هدایت به اقدام بعدی (پیش‌نمایش/
    // دانلود/بازنگری در همان نتیجه - این بخش در فرانت‌اند روی همان DocumentDto انجام می‌شود).
    public class SearchService : ISearchService
    {
        // سقف تعداد ردیف خروجی اکسل - محافظتی در برابر یک درخواست بدون فیلتر روی کل آرشیو.
        private const int MaxExportRows = 5000;
        private const int SuggestionLimit = 8;
        private const int MinSuggestionQueryLength = 2;

        private readonly IDocumentRepository _documentRepository;

        public SearchService(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository;
        }

        public async Task<PagedResult> SearchAsync(SearchDocumentsRequestModel request)
        {
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

            var query = BuildFilteredQuery(request);
            var totalCount = await query.CountAsync();

            var documents = await query
                .OrderByDescending(d => d.CreatedTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var relatedNumbers = await DocumentDtoMapper.LoadRelatedNumbersAsync(_documentRepository, documents);
            var supersededKeys = await DocumentDtoMapper.LoadSupersededKeysAsync(_documentRepository, documents.Select(d => d.Key).ToList());

            return new PagedResult
            {
                Items = documents.Select(d => DocumentDtoMapper.MapToDto(d, relatedNumbers, supersededKeys)).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<List<DocumentSuggestionDto>> SuggestAsync(string query)
        {
            var term = query?.Trim() ?? string.Empty;
            if (term.Length < MinSuggestionQueryLength)
                return new List<DocumentSuggestionDto>();

            return await _documentRepository.GetAllAsQueryable()
                .Where(d => d.IsActive && (d.Number.Contains(term) || d.Name.Contains(term)))
                .OrderBy(d => d.Number)
                .Take(SuggestionLimit)
                .Select(d => new DocumentSuggestionDto { Key = d.Key, Number = d.Number, Name = d.Name })
                .ToListAsync();
        }

        public async Task<byte[]> ExportAsync(SearchDocumentsRequestModel request)
        {
            var documents = await BuildFilteredQuery(request)
                .Include(d => d.Project)
                .Include(d => d.OrganizationalManagement)
                .Include(d => d.OrganizationalActivity)
                .Include(d => d.DocumentType)
                .OrderByDescending(d => d.CreatedTime)
                .Take(MaxExportRows)
                .ToListAsync();

            var headers = new[] { "شماره", "نام", "دسته‌بندی", "وضعیت", "پروژه", "مدیریت سازمانی", "فعالیت سازمانی", "نوع سند", "بازنگری", "تاریخ بازبینی جاری" };

            var rows = documents.Select(d => new[]
            {
                d.Number,
                d.Name,
                d.Category == HSEQ.Common.DocumentCategory.Project ? "پروژه" : "ستاد",
                d.IsActive ? "فعال" : "غیرفعال",
                d.Project?.Title ?? "",
                d.OrganizationalManagement?.Title ?? "",
                d.OrganizationalActivity?.Title ?? "",
                d.DocumentType?.Title ?? "",
                d.LastVersion.ToString() + (d.ContentRevision.HasValue ? d.ContentRevision.Value.ToString("00") : ""),
                d.CurrentReviewDate?.ToString("yyyy-MM-dd") ?? "",
            });

            return SimpleXlsxWriter.Write("اسناد", headers, rows);
        }

        // فیلترهای مشترک بین SearchAsync و ExportAsync، تا دو مسیر همیشه یک نتیجه بدهند.
        private IQueryable<Document> BuildFilteredQuery(SearchDocumentsRequestModel request)
        {
            var query = _documentRepository.GetAllAsQueryable();

            if (request.IsActive.HasValue)
                query = query.Where(d => d.IsActive == request.IsActive.Value);

            if (request.Category.HasValue)
                query = query.Where(d => d.Category == request.Category.Value);

            if (request.DocumentTypeId.HasValue)
                query = query.Where(d => d.DocumentTypeId == request.DocumentTypeId.Value);

            if (request.OrganizationalManagementId.HasValue)
                query = query.Where(d => d.OrganizationalManagementId == request.OrganizationalManagementId.Value);

            if (request.OrganizationalActivityId.HasValue)
                query = query.Where(d => d.OrganizationalActivityId == request.OrganizationalActivityId.Value);

            if (request.ProjectId.HasValue)
                query = query.Where(d => d.ProjectId == request.ProjectId.Value);

            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var term = request.Query.Trim();
                query = request.SearchInFileContent
                    ? query.Where(d => d.Number.Contains(term) || d.Name.Contains(term) || (d.ExtractedText != null && d.ExtractedText.Contains(term)))
                    : query.Where(d => d.Number.Contains(term) || d.Name.Contains(term));
            }

            if (request.OnlyLatestRevision)
            {
                // «آخرین نسخه‌ی معتبر» یعنی هیچ سند دیگری این را به‌عنوان RelatedDocumentId
                // اشاره نکرده - دقیقاً همان تعریف IsSuperseded که در DocumentDtoMapper هست.
                var supersededIds = _documentRepository.GetAllAsQueryable()
                    .Where(d => d.RelatedDocumentId.HasValue)
                    .Select(d => d.RelatedDocumentId!.Value);
                query = query.Where(d => !supersededIds.Contains(d.Key));
            }

            return query;
        }
    }
}
