using HSEQ.API.Model.Dtos;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    // فقط خواندنی، پس مثل بقیه‌ی مسیرهای گزارش‌گیری برای هر کاربر احرازهویت‌شده باز است.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<DashboardSummaryDto> GetSummary()
        {
            return await _dashboardService.GetSummaryAsync();
        }
    }
}
