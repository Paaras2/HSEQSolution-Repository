using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly IUserService _userService;
        public AuthController(IJwtService jwtService, IUserService userService)
        {
            _jwtService = jwtService;
            _userService = userService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginRequestModel model)
        {
            try
            {
                var isAuthenticated = await _userService.CheckUserAndPassword(model);
                var token = await _jwtService.GenerateJwtToken(model.Username);
                return Ok(new
                {
                    Token = token
                });
            }
            catch (ExternalAuthException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message ?? "خطایی رخ داده است",
                    code = ex.Code
                });
            }

        }
    }
}
