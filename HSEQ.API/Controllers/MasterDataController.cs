using HSEQ.API.Model.Dtos;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    // Read-only reference data for populating Document form dropdowns (Project,
    // Organizational Management/Activity, Document Type). Any authenticated user
    // can read these - they are needed to view Documents, not only to manage them.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MasterDataController : ControllerBase
    {
        private readonly IMasterDataService _masterDataService;

        public MasterDataController(IMasterDataService masterDataService)
        {
            _masterDataService = masterDataService;
        }

        [HttpGet("projects")]
        public async Task<List<ProjectLookupDto>> GetProjects()
        {
            return await _masterDataService.GetProjectsAsync();
        }

        [HttpGet("organizational-managements")]
        public async Task<List<OrganizationalManagementLookupDto>> GetOrganizationalManagements()
        {
            return await _masterDataService.GetOrganizationalManagementsAsync();
        }

        [HttpGet("organizational-activities")]
        public async Task<List<OrganizationalActivityLookupDto>> GetOrganizationalActivities()
        {
            return await _masterDataService.GetOrganizationalActivitiesAsync();
        }

        [HttpGet("document-types")]
        public async Task<List<DocumentTypeLookupDto>> GetDocumentTypes()
        {
            return await _masterDataService.GetDocumentTypesAsync();
        }
    }
}
