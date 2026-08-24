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

        // شماره‌ی مدارک مرتبط (DocumentRelations) هر سندِ فهرست، از هر دو سمت ردیف، در یک
        // رفت‌وبرگشت برای کل صفحه - نه یک درخواست به ازای هر ردیف.
        internal static async Task<Dictionary<Guid, List<string>>> LoadRelationNumbersAsync(
            IDocumentRelationRepository relationRepository,
            List<Document> documents)
        {
            var keys = documents.Select(d => d.Key).ToList();
            if (keys.Count == 0)
                return new Dictionary<Guid, List<string>>();

            var rows = await relationRepository.GetAllAsQueryable()
                .Where(r => keys.Contains(r.SourceDocumentId) || keys.Contains(r.TargetDocumentId))
                .Select(r => new
                {
                    r.SourceDocumentId,
                    r.TargetDocumentId,
                    SourceNumber = r.SourceDocument.Number,
                    TargetNumber = r.TargetDocument.Number,
                })
                .ToListAsync();

            var map = new Dictionary<Guid, List<string>>();
            void Add(Guid key, string number)
            {
                if (!map.TryGetValue(key, out var list))
                    map[key] = list = new List<string>();
                list.Add(number);
            }

            // هر ردیف از دید هر سمتی که در فهرست است، شماره‌ی «سمت مقابل» را می‌گیرد.
            foreach (var row in rows)
            {
                if (keys.Contains(row.SourceDocumentId))
                    Add(row.SourceDocumentId, row.TargetNumber);
                if (keys.Contains(row.TargetDocumentId))
                    Add(row.TargetDocumentId, row.SourceNumber);
            }

            foreach (var list in map.Values)
                list.Sort(StringComparer.Ordinal);

            return map;
        }

        internal static DocumentDto MapToDto(
            Document document,
            Dictionary<Guid, string> relatedNumbers,
            HashSet<Guid> supersededKeys,
            // اختیاری تا فراخوان‌های موجود دست نخورند؛ فقط فهرست صفحه‌بندی‌شده آن را می‌دهد.
            Dictionary<Guid, List<string>>? relationNumbers = null)
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
                IsEnglishVersion = document.IsEnglishVersion,
                RelatedDocumentId = document.RelatedDocumentId,
                RelatedDocumentNumber = document.RelatedDocumentId.HasValue
                    && relatedNumbers.TryGetValue(document.RelatedDocumentId.Value, out var relatedNumber)
                        ? relatedNumber
                        : null,
                IsSuperseded = supersededKeys.Contains(document.Key),
                RelatedDocumentNumbers = relationNumbers != null
                    && relationNumbers.TryGetValue(document.Key, out var numbers)
                        ? numbers
                        : new List<string>(),
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
