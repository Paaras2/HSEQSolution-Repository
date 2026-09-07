using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace HSEQ.Service.Services.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IFileService _fileService;
        private readonly IDocumentNumberGeneratorService _documentNumberGenerator;
        private readonly IFileTextExtractionService _fileTextExtractionService;
        private readonly IDocumentRelationService _documentRelationService;
        // برای ستون «شماره مدارک مرتبط» فهرست - خواندن دسته‌ای، جدا از سرویس ارتباط که تک‌سندی است.
        private readonly IDocumentRelationRepository _documentRelationRepository;

        public DocumentService(
            IDocumentRepository documentRepository,
            IFileService fileService,
            IDocumentNumberGeneratorService documentNumberGenerator,
            IFileTextExtractionService fileTextExtractionService,
            IDocumentRelationService documentRelationService,
            IDocumentRelationRepository documentRelationRepository)
        {
            _documentRepository = documentRepository;
            _fileService = fileService;
            _documentNumberGenerator = documentNumberGenerator;
            _fileTextExtractionService = fileTextExtractionService;
            _documentRelationService = documentRelationService;
            _documentRelationRepository = documentRelationRepository;
        }
        //Add
        public async Task<Guid> AddAsync(CreateDocumentRequestModel request, string pcode)
        {
            GeneratedDocumentNumber generated;

            if (request.Category == DocumentCategory.Project)
            {
                if (request.ProjectId is null)
                    throw new ProjectRequiredForProjectCategoryException();

                generated = await _documentNumberGenerator.GenerateAsync(
                    request.ProjectId.Value,
                    request.OrganizationalManagementId,
                    request.OrganizationalActivityId,
                    request.DocumentTypeId,
                    request.IsEnglishVersion);
            }
            else
            {
                generated = await _documentNumberGenerator.GenerateHeadquartersAsync(
                    request.OrganizationalManagementId,
                    request.OrganizationalActivityId,
                    request.DocumentTypeId,
                    request.IsEnglishVersion);
            }

            var fileName = await _fileService.SaveFileAsync(generated.Number, request.File);
            var document = new Domain.Entities.Document
            {
                CreatedByPCode = Convert.ToInt32(pcode),
                CurrentReviewDate = request.CurrentReviewDate,
                FileName = fileName,
                FormerReviewDate = request.FormerReviewDate,
                IsActive = true,
                Number = generated.Number,
                SerialNumber = generated.SerialNumber,
                LastVersion = generated.LastVersion,
                ContentRevision = generated.ContentRevision,
                IsEnglishVersion = request.IsEnglishVersion,
                Category = request.Category,
                ProjectId = request.Category == DocumentCategory.Project ? request.ProjectId : null,
                OrganizationalManagementId = request.OrganizationalManagementId,
                OrganizationalActivityId = request.OrganizationalActivityId,
                DocumentTypeId = request.DocumentTypeId,
                Name = request.Name,
                RelatedDocumentId = request.RelatedDocumentId,
                ExtractedText = ExtractTextBestEffort(request.File)
            };

            await _documentRepository.AddAsync(document);
            return document.Key;
        }

        // برای جستجوی پیشرفته در محتوای فایل. جدا از SaveFileAsync چون شکست استخراج
        // هرگز نباید مانع ذخیره‌ی خود مدرک شود؛ IFormFile.OpenReadStream چندبار قابل باز شدن است.
        private string? ExtractTextBestEffort(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return _fileTextExtractionService.Extract(stream, file.FileName);
        }
        //Update
        public async Task UpdateAsync(UpdateDocumentRequestModel request)
        {
            var document = await _documentRepository.GetByIdAsync(request.Key);
            if (document == null)
                throw new DocumentNotFoundException();

            // A superseded revision is inactive *because* a newer revision replaced it.
            // Reactivating it would leave two revisions of the same chain current at
            // once, so it is refused - the way forward is to revise the current
            // revision again, not to resurrect an old one. Only checked on the
            // inactive -> active transition, so the ordinary edit costs no extra query.
            if (request.IsActive && !document.IsActive)
            {
                var isSuperseded = await _documentRepository.GetAllAsQueryable()
                    .AnyAsync(d => d.RelatedDocumentId == document.Key);

                if (isSuperseded)
                    throw new SupersededDocumentCannotBeReactivatedException();
            }

            document.IsActive = request.IsActive;
            document.Name = request.Name;
            document.CurrentReviewDate = request.CurrentReviewDate;
            document.FormerReviewDate = request.FormerReviewDate;

            // RelatedDocumentId is deliberately NOT assigned here. It is the revision
            // chain link, written once by ReviseAsync and structural from then on -
            // assigning it from the request meant any update that omitted the field
            // silently nulled it and detached a revision from its predecessor.
            //
            // FileName is left alone for the same class of reason: an update is
            // metadata-only. Changing what the document *says* is a revision, which
            // goes through ReviseAsync and gets its own Number and its own file.

            await _documentRepository.UpdateAsync(document);
        }
        //Revise
        public async Task ReviseAsync(ReviseDocumentRequestModel request, string pcode)
        {
            var current = await _documentRepository.GetByIdAsync(request.Key);
            if (current == null)
                throw new DocumentNotFoundException();

            if (request.File == null)
                throw new RevisionFileRequiredException();

            // Keeps the chain linear. Without this, revising an already-superseded
            // document twice would mint the same next Number twice.
            var alreadySuperseded = await _documentRepository.GetAllAsQueryable()
                .AnyAsync(d => d.RelatedDocumentId == current.Key);
            if (alreadySuperseded)
                throw new DocumentAlreadyRevisedException();

            var generated = await _documentNumberGenerator.GenerateNextRevisionAsync(current);

            // Files are keyed by Document Number, and the revision has a brand-new one,
            // so this writes alongside the superseded revision's file rather than over it.
            var fileName = await _fileService.SaveFileAsync(generated.Number, request.File);

            var revision = new Domain.Entities.Document
            {
                CreatedByPCode = Convert.ToInt32(pcode),
                CreatedTime = DateTime.Now,
                IsActive = true,

                Number = generated.Number,
                SerialNumber = generated.SerialNumber,
                LastVersion = generated.LastVersion,
                ContentRevision = generated.ContentRevision,

                Name = string.IsNullOrWhiteSpace(request.Name) ? current.Name : request.Name,
                FileName = fileName,
                ExtractedText = ExtractTextBestEffort(request.File),

                // The superseded revision's review date is what the new revision revises
                // *from*, so it becomes the new FormerReviewDate unless overridden.
                FormerReviewDate = request.FormerReviewDate ?? current.CurrentReviewDate,
                CurrentReviewDate = request.CurrentReviewDate,

                // A revision is the same document, so its classification - and therefore
                // the whole base code - is inherited verbatim. برچسب نسخه‌ی انگلیسی هم
                // همراهش می‌آید، وگرنه بازنگریِ یک سند EN پسوندش را از دست می‌داد.
                IsEnglishVersion = current.IsEnglishVersion,
                Category = current.Category,
                ProjectId = current.ProjectId,
                OrganizationalManagementId = current.OrganizationalManagementId,
                OrganizationalActivityId = current.OrganizationalActivityId,
                DocumentTypeId = current.DocumentTypeId,

                // Backwards link to the revision this one supersedes.
                RelatedDocumentId = current.Key
            };

            await _documentRepository.AddAsync(revision);

            // مدارک مرتبط با زنجیره کار دارند، نه با یک بازنگریِ مشخص - و این بازنگری ردیف
            // Document تازه‌ای با کلید جدید است. پس ارتباط‌ها به نسخه‌ی جدید *منتقل* می‌شوند:
            // نه کپی (که فهرست مدرک آن‌سر را با ردیف‌های منسوخ پر می‌کرد) و نه رها (که نسخه‌ی
            // جدید را بدون هیچ مدرک مرتبطی متولد می‌کرد).
            //
            // Key نسخه‌ی جدید همین‌جا در دسترس است: کلیدهای Guid را EF سمت کلاینت هنگام
            // Add تولید می‌کند، نه پایگاه‌داده هنگام SaveChanges.
            await _documentRelationService.TransferRelationsAsync(current.Key, revision.Key);

            // The superseded revision is kept as history but drops out of the default
            // (active-only) listing. DocumentDto.IsSuperseded tells it apart from a
            // document that was deactivated outright.
            current.IsActive = false;
            await _documentRepository.UpdateAsync(current);
        }
        //Delete
        public async Task DeleteAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new DocumentNotFoundException();

            document.IsActive = false;
            await _documentRepository.UpdateAsync(document);
        }
       // GetAll
        public async Task<List<DocumentDto>> GetAllAsync(bool includeDeactiveItems = true)
        {
            throw new Exception();
        }
        //GetById
        public async Task<DocumentDto> GetByIdAsync(Guid id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
                throw new DocumentNotFoundException();

            var documents = new List<Domain.Entities.Document> { document };

            return DocumentDtoMapper.MapToDto(
                document,
                await DocumentDtoMapper.LoadRelatedNumbersAsync(_documentRepository, documents),
                await DocumentDtoMapper.LoadSupersededKeysAsync(_documentRepository, new List<Guid> { document.Key }));
        }
        //GetFile
        public async Task<DocumentFileResult> GetFileAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new DocumentNotFoundException();

            var content = _fileService.OpenDocumentFile(document.FileName);
            if (content == null)
                throw new DocumentFileNotFoundException();

            return new DocumentFileResult(content, document.FileName);
        }

        //RevisionHistory
        public async Task<List<DocumentDto>> GetRevisionHistoryAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                throw new DocumentNotFoundException();

            // زنجیره‌ی بازنگری با پیوند RelatedDocumentId ساخته می‌شود، نه با SerialNumber.
            // سریالِ اسناد ستادی برای هر کد پنج‌حرفی مستقل از صفر شمرده می‌شود، پس مثلاً
            // AGEWI-001، TSDDM-001 و IGERE-001 همگی SerialNumber = 1 دارند و شرط قبلی
            // مدارک کاملاً بی‌ربط را در تاریخچه‌ی یکدیگر نشان می‌داد.
            //
            // نامزدها با یک کوئری محدود می‌شوند به اسنادی که همان هویت پایه‌ی شماره را
            // دارند (دسته، پروژه، مدیریت، فعالیت، نوع، سریال و برچسب نسخه‌ی انگلیسی) -
            // این‌ها هنگام بازنگری عیناً کپی می‌شوند، پس هر عضو زنجیره حتماً در این مجموعه
            // هست. پیوند واقعی بعد در حافظه دنبال می‌شود، بدون رفت‌وبرگشت اضافه به دیتابیس.
            var candidates = await _documentRepository.GetAllAsQueryable()
                .Where(d => d.SerialNumber == document.SerialNumber
                    && d.Category == document.Category
                    && d.ProjectId == document.ProjectId
                    && d.OrganizationalManagementId == document.OrganizationalManagementId
                    && d.OrganizationalActivityId == document.OrganizationalActivityId
                    && d.DocumentTypeId == document.DocumentTypeId
                    && d.IsEnglishVersion == document.IsEnglishVersion)
                .ToListAsync();

            // فقط زنجیره‌ی همین مدرک: از خودش به عقب تا ریشه و به جلو تا آخرین بازنگری.
            var chain = BuildRevisionChain(document, candidates);

            var relatedNumbers = await DocumentDtoMapper.LoadRelatedNumbersAsync(_documentRepository, chain);
            var supersededKeys = await DocumentDtoMapper.LoadSupersededKeysAsync(_documentRepository, chain.Select(d => d.Key).ToList());

            return chain.Select(d => DocumentDtoMapper.MapToDto(d, relatedNumbers, supersededKeys)).ToList();
        }

        // زنجیره‌ی بازنگریِ یک سند را از میان نامزدها بیرون می‌کشد: ابتدا با دنبال‌کردن
        // RelatedDocumentId به عقب تا قدیمی‌ترین نسخه، سپس با پیمایش پیوندهای معکوس به
        // جلو تا آخرین بازنگری. خروجی از قدیمی به جدید مرتب است.
        private static List<Domain.Entities.Document> BuildRevisionChain(
            Domain.Entities.Document document,
            List<Domain.Entities.Document> candidates)
        {
            // خودِ سند حتماً در نگاشت باشد، حتی اگر کوئری نامزدها آن را برنگرداند.
            var byKey = candidates
                .GroupBy(d => d.Key)
                .ToDictionary(g => g.Key, g => g.First());
            byKey[document.Key] = document;

            // نگاشت «نسخه‌ی قبلی -> نسخه‌ی بعدی» برای پیمایش رو به جلو. زنجیره خطی است
            // (ReviseAsync بازنگری دوباره‌ی یک نسخه‌ی منسوخ را رد می‌کند)، ولی TryAdd
            // مانع از خطا روی داده‌ی قدیمیِ ناسازگار می‌شود.
            var nextByPrevious = new Dictionary<Guid, Domain.Entities.Document>();
            foreach (var candidate in byKey.Values)
            {
                if (candidate.RelatedDocumentId.HasValue && byKey.ContainsKey(candidate.RelatedDocumentId.Value))
                    nextByPrevious.TryAdd(candidate.RelatedDocumentId.Value, candidate);
            }

            // عقب‌گرد تا ریشه. visited هم تکرار را می‌گیرد هم حلقه‌ی احتمالی در داده را.
            var visited = new HashSet<Guid>();
            var chain = new List<Domain.Entities.Document>();
            var cursor = document;
            while (cursor != null && visited.Add(cursor.Key))
            {
                chain.Add(cursor);
                cursor = cursor.RelatedDocumentId.HasValue
                    && byKey.TryGetValue(cursor.RelatedDocumentId.Value, out var previous)
                        ? previous
                        : null;
            }

            // ترتیب خروجی از قدیمی به جدید است، پس عقب‌گرد باید معکوس شود.
            chain.Reverse();

            // جلو رفتن از خودِ سند تا آخرین بازنگری.
            var forward = document;
            while (nextByPrevious.TryGetValue(forward.Key, out var newer) && visited.Add(newer.Key))
            {
                chain.Add(newer);
                forward = newer;
            }

            return chain;
        }

        //Pagination
        public async Task<PagedResult> GetAllPaginationAsync(
            int pageNumber,
            int pageSize,
            bool includeDeactiveItems = true)
        {
            if (pageNumber < 1)
                pageNumber = 1;

            if (pageSize < 1)
                pageSize = 10;

            var query = _documentRepository.GetAllAsQueryable();

            if (!includeDeactiveItems)
            {
                query = query.Where(x => x.IsActive);
            }

            var totalCount = await query.CountAsync();

            var documents = await query
                .OrderBy(x => x.Key)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Both resolved in one round-trip each for the whole page, rather than
            // per row - the revision chain is a self-join the paged query can't do.
            var relatedNumbers = await DocumentDtoMapper.LoadRelatedNumbersAsync(_documentRepository, documents);
            var supersededKeys = await DocumentDtoMapper.LoadSupersededKeysAsync(_documentRepository, documents.Select(d => d.Key).ToList());
            // شماره‌ی مدارک مرتبط هر ردیف، برای ستون جدید فهرست - یک کوئری برای کل صفحه.
            var relationNumbers = await DocumentDtoMapper.LoadRelationNumbersAsync(_documentRelationRepository, documents);

            return new PagedResult
            {
                Items = documents.Select(d => DocumentDtoMapper.MapToDto(d, relatedNumbers, supersededKeys, relationNumbers)).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}