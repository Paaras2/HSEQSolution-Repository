using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace HSEQ.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly IUserService _userService;
        private readonly IHostEnvironment _env;

        // Dev-login roles are fixed, made-up test identities - not real PCodes from
        // the UM/Admins tables - so this endpoint has zero dependency on either.
        private static readonly Dictionary<string, string> DevRolePCodes = new()
        {
            ["Admin"] = "900001",
            ["DocumentManager"] = "900002",
            ["ReadOnly"] = "900003",
        };

        public AuthController(IJwtService jwtService, IUserService userService, IHostEnvironment env)
        {
            _jwtService = jwtService;
            _userService = userService;
            _env = env;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginRequestModel model)
        {
            try
            {
                var credential = await _userService.CheckUserAndPassword(model);

                // The UM service can return 200 OK with a null body, or with a body for an
                // account that is not active. Both cases must be treated as a failed login,
                // not a successful one - previously this result was never checked and a
                // token was issued regardless.
                if (credential == null || !credential.IsActive)
                {
                    return Unauthorized(new
                    {
                        message = "نام کاربری یا رمز عبور نامعتبر است",
                        code = 401
                    });
                }

                var token = await _jwtService.GenerateJwtToken(credential.PCode.ToString());
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

        // Development-only shortcut that mints a token for a made-up test identity by
        // role, with no call to the UM service or the database - so local frontend
        // work isn't blocked while the real UM host (172.17.0.254:8030) is unreachable
        // from dev machines. Disabled outside Development so it can never ship.
        [HttpPost("dev-login")]
        public async Task<IActionResult> DevLogin([FromForm] DevLoginRequestModel model)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            if (model?.Role == null || !DevRolePCodes.TryGetValue(model.Role, out var pcode))
            {
                return BadRequest(new
                {
                    message = "Unknown dev role. Expected one of: " + string.Join(", ", DevRolePCodes.Keys),
                    code = 400
                });
            }

            var token = await _jwtService.GenerateDevToken(pcode, model.Role);
            return Ok(new
            {
                Token = token
            });
        }
    }
}