using HSEQ.Common;
using HSEQ.Domain.DocumentNumbering;
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
        private readonly IDocumentNumberingRepository _numberingRepository;

        public DocumentNumberGeneratorService(IDocumentNumberingRepository numberingRepository)
        {
            _numberingRepository = numberingRepository;
        }

        public async Task<GeneratedDocumentNumber> GenerateAsync(
            Guid projectId,
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId)
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

            // Defensive only: the SEQUENCE-backed serial makes a collision structurally
            // impossible for a freshly generated code, but application-level validation
            // is explicitly required in addition to the database unique constraint.
            if (await _numberingRepository.NumberExistsAsync(code.Value))
                throw new DuplicateDocumentNumberException();

            return new GeneratedDocumentNumber(code.Value, serial, revision.Major, revision.ContentRevision);
        }
    }
}