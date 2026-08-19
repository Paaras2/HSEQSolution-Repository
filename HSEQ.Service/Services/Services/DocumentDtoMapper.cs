using HSEQ.API.Model.Dtos;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // منطق مشترک تبدیل Document به DocumentDto - از DocumentService به اینجا منتقل شد تا
    // SearchService هم بدون کپی‌کردن دوباره‌ی همین منطق از آن استفاده کند.
    internal static class DocumentDtoMapper
    {
        // شماره‌ی مدرک قبلی هر ردیف (برای RelatedDocumentNumber)، در یک رفت‌وبرگشت به
        // دیتابیس به‌جای یک بار به ازای هر ردیف.
        internal static async Task<Dictionary<Guid, string>> LoadRelatedNumbersAsync(
            IDocumentRepository documentRepository,
            List<Document> documents)
        {
            var relatedIds = documents
                .Where(d => d.RelatedDocumentId.HasValue)
                .Select(d => d.RelatedDocumentId!.Value)
                .Distinct()
                .ToList();

            if (relatedIds.Count == 0)
                return new Dictionary<Guid, string>();

            return await documentRepository.GetAllAsQueryable()
                .Where(d => relatedIds.Contains(d.Key))
                .ToDictionaryAsync(d => d.Key, d => d.Number);
        }

        // زیرمجموعه‌ای از کلیدهای داده‌شده که یک بازنگری جدیدتر جایگزینشان کرده است.
        internal static async Task<HashSet<Guid>> LoadSupersededKeysAsync(
            IDocumentRepository documentRepository,
            List<Guid> keys)
        {
            if (keys.Count == 0)
                return new HashSet<Guid>();

            var superseded = await documentRepository.GetAllAsQueryable()
                .Where(d => d.RelatedDocumentId.HasValue && keys.Contains(d.RelatedDocumentId.Value))
                .Select(d => d.RelatedDocumentId!.Value)
                .Distinct()
                .ToListAsync();

            return superseded.ToHashSet();
        }

        internal static DocumentDto MapToDto(
            Document document,
            Dictionary<Guid, string> relatedNumbers,
            HashSet<Guid> supersededKeys)
        {
            return new DocumentDto
            {
                Key = document.Key,
                IsActive = document.IsActive,
                CreatedTime = document.CreatedTime,
                ModifiedDate = document.ModifiedDate,
                Number = document.Number,
                Name = document.Name,
                FormerReviewDate = document.FormerReviewDate,
                CurrentReviewDate = document.CurrentReviewDate,
                LastVersion = document.LastVersion,
                ContentRevision = document.ContentRevision,
                SerialNumber = document.SerialNumber,
                RelatedDocumentId = document.RelatedDocumentId,
                RelatedDocumentNumber = document.RelatedDocumentId.HasValue
                    && relatedNumbers.TryGetValue(document.RelatedDocumentId.Value, out var relatedNumber)
                        ? relatedNumber
                        : null,
                IsSuperseded = supersededKeys.Contains(document.Key),
                FileName = document.FileName,
                Category = document.Category,
                ProjectId = document.ProjectId,
                OrganizationalManagementId = document.OrganizationalManagementId,
                OrganizationalActivityId = document.OrganizationalActivityId,
                DocumentTypeId = document.DocumentTypeId,
                File = null,
                CreatedByPCode = document.CreatedByPCode
            };
        }
    }
}
