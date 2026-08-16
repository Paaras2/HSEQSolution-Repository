using HSEQ.API.Model.Dtos;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;

namespace HSEQ.Service.Services.Services
{
    public class MasterDataService : IMasterDataService
    {
        private readonly IMasterDataRepository _masterDataRepository;

        public MasterDataService(IMasterDataRepository masterDataRepository)
        {
            _masterDataRepository = masterDataRepository;
        }

        public async Task<List<ProjectLookupDto>> GetProjectsAsync()
        {
            var projects = await _masterDataRepository.GetActiveProjectsAsync();
            return projects.Select(p => new ProjectLookupDto
            {
                Key = p.Key,
                Code = p.Code,
                Title = p.Title,
                IsProjectRelated = p.IsProjectRelated
            }).ToList();
        }

        public async Task<List<OrganizationalManagementLookupDto>> GetOrganizationalManagementsAsync()
        {
            var managements = await _masterDataRepository.GetActiveManagementsAsync();
            return managements.Select(m => new OrganizationalManagementLookupDto
            {
                Key = m.Key,
                Code = m.Code,
                Title = m.Title
            }).ToList();
        }

        public async Task<List<OrganizationalActivityLookupDto>> GetOrganizationalActivitiesAsync()
        {
            var activities = await _masterDataRepository.GetActiveActivitiesAsync();
            return activities.Select(a => new OrganizationalActivityLookupDto
            {
                Key = a.Key,
                Code = a.Code,
                Title = a.Title,
                OrganizationalManagementId = a.OrganizationalManagementId
            }).ToList();
        }

        public async Task<List<DocumentTypeLookupDto>> GetDocumentTypesAsync()
        {
            var types = await _masterDataRepository.GetActiveDocumentTypesAsync();
            return types.Select(t => new DocumentTypeLookupDto
            {
                Key = t.Key,
                Code = t.Code,
                Title = t.Title
            }).ToList();
        }
    }
}
