using System;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Services
{
    // Generates a new, complete, immutable Document Number for a Document being created.
    // Not used for updates - Document Number never changes after creation.
    public interface IDocumentNumberGeneratorService
    {
        Task<GeneratedDocumentNumber> GenerateAsync(
            Guid projectId,
            Guid organizationalManagementId,
            Guid organizationalActivityId,
            Guid documentTypeId);
    }
}