using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _isUserAdminService;

        public AdminController(IAdminService isUserAdminService)
        {
            _isUserAdminService = isUserAdminService;
        }

        [HttpGet]
        [Route("Get")]
        public async Task<bool> Get(int pcode)
        {
            return await _isUserAdminService.IsAdminAsync(pcode);
        }
    }
}
