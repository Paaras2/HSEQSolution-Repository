using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Domain.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    // Read-only reference data for populating Document form dropdowns (Project,
    // Organizational Management/Activity, Document Type). Any authenticated user
    // can read these - they are needed to view Documents, not only to manage them.
    [Route("[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MasterDataController : ControllerBase
    {
        private readonly IMasterDataService _masterDataService;
        private readonly IUnitOfWork _unitOfWork;

        // نوشتن اطلاعات پایه برای هر دو نقشِ پنل ادمین باز است؛ خواندن (متدهای زیر)
        // مثل قبل برای هر کاربر احرازهویت‌شده.
        private const string AdminPanelRoles = "Admin,DocumentManager";

        public MasterDataController(IMasterDataService masterDataService, IUnitOfWork unitOfWork)
        {
            _masterDataService = masterDataService;
            _unitOfWork = unitOfWork;
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

        // ---- پنل ادمین ----
        // یک مسیر مشترک برای هر چهار نوع؛ نوع با پارامتر kind مشخص می‌شود.

        [Authorize(Roles = AdminPanelRoles)]
        [HttpGet("admin/list")]
        public async Task<IActionResult> GetAllForAdmin([FromQuery] MasterDataKind kind)
        {
            var items = await _masterDataService.GetAllForAdminAsync(kind);
            return Ok(items);
        }

        [Authorize(Roles = AdminPanelRoles)]
        [HttpPost("admin/create")]
        public async Task<IActionResult> Create([FromForm] CreateMasterDataRequestModel request)
        {
            await _masterDataService.CreateAsync(request);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }

        // آرشیو شماره‌مدارک قدیمی - فقط خواندنی، پس فقط GET دارد.
        [Authorize(Roles = AdminPanelRoles)]
        [HttpGet("admin/legacy-numbers")]
        public async Task<LegacyDocumentNumberPagedResult> GetLegacyDocumentNumbers(
            [FromQuery] LegacyDocumentNumberQueryRequestModel request)
        {
            return await _masterDataService.GetLegacyDocumentNumbersAsync(request);
        }

        [Authorize(Roles = AdminPanelRoles)]
        [HttpPost("admin/update")]
        public async Task<IActionResult> Update([FromForm] UpdateMasterDataRequestModel request)
        {
            await _masterDataService.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }
    }
}
