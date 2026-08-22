using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;

namespace HSEQ.Service.Services.Services
{
    // فیلتر مشترک «فقط آخرین بازنگری» - از SearchService به اینجا منتقل شد تا
    // DashboardService هم بدون کپی‌کردن دوباره از همین منطق استفاده کند.
    internal static class DocumentQueryExtensions
    {
        // یعنی هیچ سند دیگری این را به‌عنوان RelatedDocumentId اشاره نکرده (منسوخ نشده) -
        // همان تعریف IsSuperseded در DocumentDtoMapper.
        internal static IQueryable<Document> WhereLatestRevision(this IQueryable<Document> query, IDocumentRepository documentRepository)
        {
            var supersededIds = documentRepository.GetAllAsQueryable()
                .Where(d => d.RelatedDocumentId.HasValue)
                .Select(d => d.RelatedDocumentId!.Value);

            return query.Where(d => !supersededIds.Contains(d.Key));
        }
    }
}
