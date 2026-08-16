using HSEQ.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Repositories
{
    // Data access needed to generate a Document Number: master-data lookups, the
    // global serial allocation, and the defensive uniqueness check. Kept separate
    // from IDocumentRepository because it serves a distinct concern (numbering,
    // not Document CRUD) and touches several master-data tables, not just Documents.
    public interface IDocumentNumberingRepository
    {
        Task<Project?> GetActiveProjectAsync(Guid projectId);
        Task<OrganizationalManagement?> GetActiveManagementAsync(Guid managementId);

        // Also confirms the Activity actually belongs to the given Management,
        // since Activity codes are only meaningful in combination with their Management.
        Task<OrganizationalActivity?> GetActiveActivityAsync(Guid activityId, Guid managementId);

        Task<DocumentType?> GetActiveDocumentTypeAsync(Guid documentTypeId);

        // Allocates the next value from the global SQL Server SEQUENCE (SSS component).
        // Never derives the value from existing Documents rows.
        Task<int> AllocateNextSerialNumberAsync();

        // Defensive check only - the SEQUENCE-backed serial guarantees uniqueness by
        // construction. Present because application-level validation is explicitly required
        // in addition to the database unique constraint.
        Task<bool> NumberExistsAsync(string number);
    }
}