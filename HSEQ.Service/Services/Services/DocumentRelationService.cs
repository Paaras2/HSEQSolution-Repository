using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // «مدارک مرتبط»: شبکه‌ی ارجاع میان مدارک مستقل - دستورالعمل و فرمِ آن، روش اجرایی و
    // خط‌مشی بالادستی، مدرکی که جایگزین مدرک دیگری شده.
    //
    // دو تصمیم طراحی که رفتار این سرویس را توضیح می‌دهند:
    //
    //   ۱) ارتباط، جفتِ بدون‌ترتیب است. ردیف جهت‌دار ذخیره می‌شود (چون «مرجع» و «پیوست»
    //      جهت دارند)، اما همان جفت در جهت معکوس دوباره ثبت نمی‌شود؛ وگرنه دو ردیفِ
    //      «A مرجعِ B» و «B مرجعِ A» می‌توانستند هم‌زمان وجود داشته باشند.
    //
    //   ۲) ارتباط، در عمل میان دو *زنجیره‌ی* مدرک است، نه دو بازنگریِ مشخص. چون هر بازنگری
    //      در این سامانه یک ردیف Document مستقل با کلید تازه است، ReviseAsync ارتباط‌ها را
    //      به نسخه‌ی جدید منتقل می‌کند (TransferRelationsAsync) - منتقل، نه کپی. اگر کپی
    //      می‌شدند، فهرست مدرک آن‌سرِ ارتباط بعد از هر بازنگری یک ردیف منسوخ اضافه
    //      می‌گرفت و بعد از چند بازنگری خواندنش ناممکن می‌شد؛ بدتر از آن، کاربر می‌توانست
    //      از روی ردیف کهنه فایل نسخه‌ی از رده خارج را دانلود کند.
    public class DocumentRelationService : IDocumentRelationService
    {
        private readonly IDocumentRelationRepository _relationRepository;
        private readonly IDocumentRepository _documentRepository;

        public DocumentRelationService(
            IDocumentRelationRepository relationRepository,
            IDocumentRepository documentRepository)
        {
            _relationRepository = relationRepository;
            _documentRepository = documentRepository;
        }

        public async Task<List<DocumentRelationDto>> GetForDocumentAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new DocumentNotFoundException();

            var relations = await _relationRepository.GetAllAsQueryable()
                .Where(r => r.SourceDocumentId == documentId || r.TargetDocumentId == documentId)
                .Include(r => r.SourceDocument)
                .Include(r => r.TargetDocument)
                .OrderBy(r => r.RelationType)
                .ThenBy(r => r.CreatedTime)
                .ToListAsync();

            if (relations.Count == 0)
                return new List<DocumentRelationDto>();

            // همان تعریف IsSuperseded در DocumentDtoMapper، اما برای مدارکِ سمت مقابل -
            // در یک رفت‌وبرگشت برای کل فهرست، نه یکی به ازای هر ردیف.
            var otherIds = relations
                .Select(r => r.SourceDocumentId == documentId ? r.TargetDocumentId : r.SourceDocumentId)
                .Distinct()
                .ToList();

            var supersededKeys = await DocumentDtoMapper.LoadSupersededKeysAsync(_documentRepository, otherIds);

            return relations.Select(r =>
            {
                var isOutgoing = r.SourceDocumentId == documentId;
                var other = isOutgoing ? r.TargetDocument : r.SourceDocument;

                return new DocumentRelationDto
                {
                    Key = r.Key,
                    DocumentId = other.Key,
                    Number = other.Number,
                    Name = other.Name,
                    IsActive = other.IsActive,
                    IsSuperseded = supersededKeys.Contains(other.Key),
                    RelationType = r.RelationType,
                    IsOutgoing = isOutgoing,
                    Note = r.Note,
                    CreatedTime = r.CreatedTime,
                    CreatedByPCode = r.CreatedByPCode
                };
            }).ToList();
        }

        public async Task AddAsync(CreateDocumentRelationRequestModel request, string pcode)
        {
            if (request.SourceDocumentId == request.TargetDocumentId)
                throw new SelfDocumentRelationException();

            // هر دو سر ارتباط باید واقعاً وجود داشته باشند - وگرنه FK همین را با یک خطای
            // پایگاه‌داده‌ی نامفهوم اعلام می‌کرد.
            if (await _documentRepository.GetByIdAsync(request.SourceDocumentId) == null)
                throw new DocumentNotFoundException();

            if (await _documentRepository.GetByIdAsync(request.TargetDocumentId) == null)
                throw new DocumentNotFoundException();

            // جفت بدون‌ترتیب: ایندکس یکتا فقط جهتِ ذخیره‌شده را می‌گیرد، پس جهت معکوس
            // اینجا بررسی می‌شود.
            var alreadyRelated = await _relationRepository.GetAllAsQueryable()
                .AnyAsync(r =>
                    (r.SourceDocumentId == request.SourceDocumentId && r.TargetDocumentId == request.TargetDocumentId) ||
                    (r.SourceDocumentId == request.TargetDocumentId && r.TargetDocumentId == request.SourceDocumentId));

            if (alreadyRelated)
                throw new DuplicateDocumentRelationException();

            await _relationRepository.AddAsync(new DocumentRelation
            {
                SourceDocumentId = request.SourceDocumentId,
                TargetDocumentId = request.TargetDocumentId,
                RelationType = request.RelationType,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedByPCode = Convert.ToInt32(pcode),
                CreatedTime = DateTime.Now,
                IsActive = true
            });
        }

        // حذف فیزیکی، برخلاف Document که فقط غیرفعال می‌شود: یک ارتباط، رکورد کنترل‌شده
        // نیست و نگه‌داشتن ردیفِ غیرفعالش هیچ اطلاعاتی به تاریخچه‌ی مدرک اضافه نمی‌کند -
        // در عوض هر پرس‌وجوی این جدول را مجبور به فیلتر IsActive می‌کرد.
        public async Task RemoveAsync(Guid relationId)
        {
            var relation = await _relationRepository.GetByIdAsync(relationId);
            if (relation == null)
                throw new DocumentRelationNotFoundException();

            await _relationRepository.DeleteAsync(relation);
        }

        public async Task TransferRelationsAsync(Guid fromDocumentId, Guid toDocumentId)
        {
            var existing = await _relationRepository.GetAllAsQueryable()
                .Where(r => r.SourceDocumentId == fromDocumentId || r.TargetDocumentId == fromDocumentId)
                .ToListAsync();

            if (existing.Count == 0)
                return;

            // فقط سرِ مربوط به نسخه‌ی قدیمی جابه‌جا می‌شود؛ جهت، نوع، توضیح و ثبت‌کننده‌ی
            // اصلی دست‌نخورده می‌مانند تا معنای «مرجع/پیوست/جایگزین» معکوس یا بی‌صاحب نشود.
            //
            // ایندکس یکتای (Source, Target) اینجا نمی‌شکند: نسخه‌ی جدید تازه ساخته شده و
            // هیچ ارتباطی از قبل به آن اشاره نمی‌کند.
            foreach (var relation in existing)
            {
                if (relation.SourceDocumentId == fromDocumentId)
                    relation.SourceDocumentId = toDocumentId;

                if (relation.TargetDocumentId == fromDocumentId)
                    relation.TargetDocumentId = toDocumentId;

                relation.ModifiedDate = DateTime.Now;
            }

            await _relationRepository.UpdateRangeAsync(existing);
        }
    }
}
