using HSEQ.Domain.Entities;

namespace HSEQ.Service.Interfaces.Repositories
{
    // Read-only master-data lookups needed to populate Document form dropdowns.
    // Kept separate from IDocumentNumberingRepository (which is part of the frozen
    // Document Numbering subsystem and only resolves single active records by id
    // for number generation) - this repository only ever lists active records
    // for display/selection.
    public interface IMasterDataRepository
    {
        Task<List<Project>> GetActiveProjectsAsync();
        Task<List<OrganizationalManagement>> GetActiveManagementsAsync();
        Task<List<OrganizationalActivity>> GetActiveActivitiesAsync();
        Task<List<DocumentType>> GetActiveDocumentTypesAsync();
    }
}
