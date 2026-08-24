using HSEQ.Common;
using HSEQ.Domain.DocumentNumbering;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using System;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Services
{
    // Orchestrates Document Number generation:
    //   1. Validate the referenced master data exists and is active.
    //   2. Allocate the next global serial number.
    //   3. Determine the initial revision (A, or A01 for project-related documents).
    //   4. Construct and structurally validate the final code (HSEQ.Domain.DocumentNumbering).
    //   5. Defensively confirm the result isn't already in use.
    public class DocumentNumberGeneratorService : IDocumentNumberGeneratorService
    {
        // پسوند نسخه‌ی انگلیسی. عمداً بیرون از value objectهای DocumentCode و
        // HeadquartersDocumentCode اعمال می‌شود: آن‌ها فقط ساختار استانداردِ شماره را
        // می‌سازند و اعتبارسنجی می‌کنند، و این یک برچسبِ نسخه است نه جزئی از کد.
        private const string EnglishVersionSuffix = " (EN)";

        private static string ApplyEnglishSuffix(string number, bool isEnglishVersion) =>
            isEnglishVersion ? number + EnglishVersionSuffix : number;

        private readonly IDocumentNumberingRepository _numberingRepository;

        public DocumentNumberGeneratorService(IDocumentNumberingRepository numberingRepository)
        {
            _numberingRepository = numberingRepository;
        }

        public async Task<GeneratedDocumentNumber> GenerateAsync(
            Guid projectId,
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId,
            bool isEnglishVersion)
        {
            var project = await _numberingRepository.GetActiveProjectAsync(projectId)
                ?? throw new ProjectNotFoundException();

            var management = await _numberingRepository.GetActiveManagementAsync(organizationalManagementId)
                ?? throw new OrganizationalManagementNotFoundException();

            var activity = await _numberingRepository.GetActiveActivityAsync(organizationalActivityId, organizationalManagementId)
                ?? throw new OrganizationalActivityNotFoundException();

            var documentType = await _numberingRepository.GetActiveDocumentTypeAsync(documentTypeId)
                ?? throw new DocumentTypeNotFoundException();

            var serial = await _numberingRepository.AllocateNextSerialNumberAsync();

            var revision = RevisionCode.Initial(project.IsProjectRelated);

            var code = DocumentCode.Create(
                project.Code,
                management.Code,
                activity.Code,
                documentType.Code,
                serial,
                revision);

            // بررسی یکتایی روی مقدار نهایی (با پسوند) انجام می‌شود، نه کدِ خام.
            var number = ApplyEnglishSuffix(code.Value, isEnglishVersion);

            // Defensive only: the SEQUENCE-backed serial makes a collision structurally
            // impossible for a freshly generated code, but application-level validation
            // is explicitly required in addition to the database unique constraint.
            if (await _numberingRepository.NumberExistsAsync(number))
                throw new DuplicateDocumentNumberException();

            return new GeneratedDocumentNumber(number, serial, revision.Major, revision.ContentRevision);
        }

        // Same shape as GenerateAsync, minus the Project: master-data lookups for
        // Management/Activity/DocumentType, allocate a serial, build the initial (A)
        // revision, assemble and defensively re-check the code. The one real difference
        // is *where* the serial comes from - the per-code counter, not the global
        // Project sequence - because Headquarters codes each run their own 001, 002, ...
        // independent of every other code, matching the legacy Excel register.
        public async Task<GeneratedDocumentNumber> GenerateHeadquartersAsync(
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId,
            bool isEnglishVersion)
        {
            var management = await _numberingRepository.GetActiveManagementAsync(organizationalManagementId)
                ?? throw new OrganizationalManagementNotFoundException();

            var activity = await _numberingRepository.GetActiveActivityAsync(organizationalActivityId, organizationalManagementId)
                ?? throw new OrganizationalActivityNotFoundException();

            var documentType = await _numberingRepository.GetActiveDocumentTypeAsync(documentTypeId)
                ?? throw new DocumentTypeNotFoundException();

            var code5 = $"{management.Code}{activity.Code}{documentType.Code}".ToUpperInvariant();
            var serial = await _numberingRepository.AllocateNextHeadquartersSerialAsync(code5);

            var revision = RevisionCode.Initial(isProjectRelated: false);

            var code = HeadquartersDocumentCode.Create(
                management.Code,
                activity.Code,
                documentType.Code,
                serial,
                revision);

            var number = ApplyEnglishSuffix(code.Value, isEnglishVersion);

            if (await _numberingRepository.NumberExistsAsync(number))
                throw new DuplicateDocumentNumberException();

            return new GeneratedDocumentNumber(number, serial, revision.Major, revision.ContentRevision);
        }

        // Same steps as GenerateAsync, with two deliberate differences:
        //   - the serial number is carried over from the document being revised
        //     instead of being allocated from the sequence (a revision is the same
        //     document, so PPPP+O+AA+DD+SSS must stay byte-identical), and
        //   - the revision component is advanced from the document's current one
        //     rather than reset to the initial value.
        public async Task<GeneratedDocumentNumber> GenerateNextRevisionAsync(Document currentDocument)
        {
            if (currentDocument is null)
                throw new ArgumentNullException(nameof(currentDocument));

            if (currentDocument.Category == DocumentCategory.Headquarters)
                return await GenerateHeadquartersNextRevisionAsync(currentDocument);

            var project = await _numberingRepository.GetActiveProjectAsync(currentDocument.ProjectId!.Value)
                ?? throw new ProjectNotFoundException();

            var management = await _numberingRepository.GetActiveManagementAsync(currentDocument.OrganizationalManagementId)
                ?? throw new OrganizationalManagementNotFoundException();

            var activity = await _numberingRepository.GetActiveActivityAsync(
                    currentDocument.OrganizationalActivityId,
                    currentDocument.OrganizationalManagementId)
                ?? throw new OrganizationalActivityNotFoundException();

            var documentType = await _numberingRepository.GetActiveDocumentTypeAsync(currentDocument.DocumentTypeId)
                ?? throw new DocumentTypeNotFoundException();

            // Rebuilding the current revision through RevisionCode.Create also validates
            // that the stored Major/ContentRevision pair still matches the project's
            // scheme, so a document whose Project.IsProjectRelated was flipped after
            // creation fails loudly here instead of silently producing a malformed code.
            var currentRevision = RevisionCode.Create(
                currentDocument.LastVersion,
                currentDocument.ContentRevision,
                project.IsProjectRelated);

            var nextRevision = currentRevision.Next(project.IsProjectRelated);

            var code = DocumentCode.Create(
                project.Code,
                management.Code,
                activity.Code,
                documentType.Code,
                currentDocument.SerialNumber,
                nextRevision);

            // بازنگریِ یک نسخه‌ی انگلیسی باز هم انگلیسی است، پس پسوند از خودِ سند می‌آید.
            var number = ApplyEnglishSuffix(code.Value, currentDocument.IsEnglishVersion);

            // Unlike the create path, this is a real guard rather than a defensive one:
            // nothing structurally prevents two concurrent revisions of the same document
            // from computing the same next Number.
            if (await _numberingRepository.NumberExistsAsync(number))
                throw new DuplicateDocumentNumberException();

            return new GeneratedDocumentNumber(
                number,
                currentDocument.SerialNumber,
                nextRevision.Major,
                nextRevision.ContentRevision);
        }

        // GenerateNextRevisionAsync's Headquarters counterpart: same SerialNumber
        // (never reallocated - a revision is the same document), revision advanced with
        // isProjectRelated: false since a Headquarters document is never project-related.
        private async Task<GeneratedDocumentNumber> GenerateHeadquartersNextRevisionAsync(Document currentDocument)
        {
            var management = await _numberingRepository.GetActiveManagementAsync(currentDocument.OrganizationalManagementId)
                ?? throw new OrganizationalManagementNotFoundException();

            var activity = await _numberingRepository.GetActiveActivityAsync(
                    currentDocument.OrganizationalActivityId,
                    currentDocument.OrganizationalManagementId)
                ?? throw new OrganizationalActivityNotFoundException();

            var documentType = await _numberingRepository.GetActiveDocumentTypeAsync(currentDocument.DocumentTypeId)
                ?? throw new DocumentTypeNotFoundException();

            var currentRevision = RevisionCode.Create(
                currentDocument.LastVersion,
                currentDocument.ContentRevision,
                isProjectRelated: false);

            var nextRevision = currentRevision.Next(isProjectRelated: false);

            var code = HeadquartersDocumentCode.Create(
                management.Code,
                activity.Code,
                documentType.Code,
                currentDocument.SerialNumber,
                nextRevision);

            var number = ApplyEnglishSuffix(code.Value, currentDocument.IsEnglishVersion);

            if (await _numberingRepository.NumberExistsAsync(number))
                throw new DuplicateDocumentNumberException();

            return new GeneratedDocumentNumber(
                number,
                currentDocument.SerialNumber,
                nextRevision.Major,
                nextRevision.ContentRevision);
        }
    }
}