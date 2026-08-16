using HSEQ.API.Model.Dtos;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IMasterDataService
    {
        Task<List<ProjectLookupDto>> GetProjectsAsync();
        Task<List<OrganizationalManagementLookupDto>> GetOrganizationalManagementsAsync();
        Task<List<OrganizationalActivityLookupDto>> GetOrganizationalActivitiesAsync();
        Task<List<DocumentTypeLookupDto>> GetDocumentTypesAsync();
    }
}
