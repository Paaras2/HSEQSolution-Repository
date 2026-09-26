using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Services
{
    // لایه‌ی چرخه‌ی حیاتِ «رزرو کد». تولیدِ شماره و جلوگیری از تکرار همان مسیرِ موجود است
    // (SEQUENCE برای پروژه، UPDATE...OUTPUT برای ستاد، و ایندکسِ یکتای Number به‌عنوان
    // پشتوانه). اینجا فقط وضعیت‌ها و انقضا اضافه می‌شود.
    public class DocumentReservationService : IDocumentReservationService
    {
        // مهلتِ پیش‌فرضِ رزرو: تا این مدت اگر فایلی ضمیمه نشود، رزرو منقضی می‌شود.
        private static readonly TimeSpan ReservationTtl = TimeSpan.FromHours(72);

        private readonly ApplicationDbContext _context;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentNumberGeneratorService _documentNumberGenerator;
        private readonly IFileService _fileService;

        public DocumentReservationService(
            ApplicationDbContext context,
            IDocumentRepository documentRepository,
            IDocumentNumberGeneratorService documentNumberGenerator,
            IFileService fileService)
        {
            _context = context;
            _documentRepository = documentRepository;
            _documentNumberGenerator = documentNumberGenerator;
            _fileService = fileService;
        }

        public async Task<Document> ReserveAsync(ReserveDocumentRequestModel request, string pcode)
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

            var document = new Document
            {
                CreatedByPCode = Convert.ToInt32(pcode),
                Name = request.Name,
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
                IsActive = true,

                // رزرو: کد گرفته شده، فایل هنوز نه. FileName خالی (نه null) نگه داشته می‌شود چون
                // ستون non-null است و FileService هم مقدارِ خالی/فاصله را «بدون فایل» می‌خواند.
                Status = DocumentStatus.Reserved,
                FileName = string.Empty,
                ReservationExpiresAtUtc = DateTime.UtcNow.Add(ReservationTtl),
            };

            // ذخیره (SaveChanges) در کنترلر با IUnitOfWork انجام می‌شود، مثل AddAsync.
            // ایندکسِ یکتای Number همین‌جا کد را در برابرِ تکرارِ همزمان «رزرو» می‌کند.
            await _documentRepository.AddAsync(document);
            return document;
        }

        public async Task AttachFileAsync(AttachDocumentFileRequestModel request)
        {
            if (request.File is null)
                throw new DocumentFileRequiredException();

            var document = await _documentRepository.GetByIdAsync(request.Key);
            if (document is null)
                throw new DocumentNotFoundException();

            if (document.Status != DocumentStatus.Reserved)
                throw new InvalidDocumentStateException("فقط مدرکِ رزروشده می‌تواند فایل بگیرد.");

            if (document.ReservationExpiresAtUtc.HasValue
                && document.ReservationExpiresAtUtc.Value < DateTime.UtcNow)
                throw new InvalidDocumentStateException("مهلتِ رزروِ این کد گذشته است؛ دوباره رزرو کنید.");

            // فایل با شماره‌ی همین مدرک ذخیره می‌شود - همان قراردادِ نام‌گذاریِ Add/Revise.
            var fileName = await _fileService.SaveFileAsync(document.Number, request.File);

            document.FileName = fileName;
            document.Status = DocumentStatus.Published;   // فایل ضمیمه شد = مدرکِ زنده
            document.ReservationExpiresAtUtc = null;      // دیگر مشمولِ انقضا نیست

            await _documentRepository.UpdateAsync(document);
        }

        public async Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default)
        {
            // یک UPDATEِ مجموعه‌ای و idempotent: ردیف‌هایی که قبلاً منقضی شده‌اند دیگر با شرط
            // نمی‌خوانند، پس اجرای همزمان از چند نمونه هم بی‌خطر است. بدونِ بارگذاریِ موجودیت‌ها.
            var now = DateTime.UtcNow;
            return await _context.Set<Document>()
                .Where(d => d.Status == DocumentStatus.Reserved
                         && d.ReservationExpiresAtUtc != null
                         && d.ReservationExpiresAtUtc < now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.Status, DocumentStatus.Expired)
                    .SetProperty(d => d.IsActive, false), cancellationToken);
        }
    }
}
