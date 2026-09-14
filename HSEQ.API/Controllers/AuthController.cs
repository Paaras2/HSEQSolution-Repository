using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HSEQ.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly IUserService _userService;
        private readonly IHostEnvironment _env;
        private readonly ILogger<AuthController> _logger;

        // Dev-login roles are fixed, made-up test identities - not real PCodes from
        // the UM/Admins tables - so this endpoint has zero dependency on either.
        private static readonly Dictionary<string, string> DevRolePCodes = new()
        {
            ["Admin"] = "900001",
            ["DocumentManager"] = "900002",
            ["ReadOnly"] = "900003",
        };

        private static readonly JsonSerializerOptions LoginJson = new() { PropertyNameCaseInsensitive = true };

        private const string UnavailableMessage =
            "ارتباط با سرویس احراز هویت برقرار نشد. لطفاً چند لحظه بعد دوباره تلاش کنید.";

        public AuthController(IJwtService jwtService, IUserService userService, IHostEnvironment env,
                              ILogger<AuthController> logger)
        {
            _jwtService = jwtService;
            _userService = userService;
            _env = env;
            _logger = logger;
        }

        // ---------------------------------------------------------------------
        // ورود
        // ---------------------------------------------------------------------
        //
        // بدنه‌ی درخواست همان قراردادِ checkCredential سامانه‌ی مدیریت کاربران است و
        // کلاینت همین را به‌صورت JSON می‌فرستد:
        //
        //     { "username": "3256", "password": "..." }
        //
        // شکلِ فرم (x-www-form-urlencoded با Username/Password) هم پذیرفته می‌ماند: تبی
        // که پیش از استقرار باز بوده هنوز کلاینت قبلی را اجرا می‌کند، و ورودش نباید فقط
        // به همین دلیل شکست بخورد. بدنه دستی خوانده می‌شود، نه با [FromBody]/[FromForm]،
        // تا هر شکلِ ناقص - بدون Content-Type، JSON خراب - به همان پاسخِ
        // «missing_fields» برسد، نه به ۴۱۵ یا ProblemDetails که صفحه‌ی ورود نمی‌شناسد.
        //
        // مرورگر هرگز خودش checkCredential را صدا نمی‌زند. پاسخ آن سرویس فقط می‌گوید
        // «این رمز درست است»؛ اگر مرورگر آن را بگیرد و به ما برساند، هر کسی می‌تواند یک
        // pCode دلخواه جعل کند. پس اعتبار را همین‌جا سرور می‌سنجد و توکن را خودش امضا
        // می‌کند.
        //
        // پاسخِ خطا یک «reason» ثابت دارد تا کلاینت بدون تکیه بر متن یا کدِ داخلیِ UM
        // (که برای رمز غلط ۴۱۰ است) تصمیم بگیرد چه نشان دهد:
        //
        //     missing_fields        ۴۰۰  کد پرسنلی یا رمز خالی است
        //     invalid_credentials   ۴۰۰  سامانه‌ی UM اعتبارنامه را نپذیرفت - پیامِ خودش می‌رسد
        //     unavailable           ۴۰۰  سامانه‌ی UM در دسترس نبود یا پاسخ نامعتبر داد
        //     inactive              ۴۰۳  رمز درست است ولی حساب در UM غیرفعال است
        //
        // کدِ ۴۰۰ برای خطاهای UM عمداً همان مقدارِ پیشین است؛ صفحه‌ی ورود و آزمون‌های
        // استقرار روی آن بنا شده‌اند.
        [HttpPost("login")]
        public async Task<IActionResult> Login()
        {
            var request = await ReadLoginRequestAsync(Request);
            var username = NormalizeUsername(request.Username);
            var password = request.Password;

            if (username.Length == 0 || string.IsNullOrEmpty(password))
                return LoginFailure(StatusCodes.Status400BadRequest, "missing_fields",
                                    "کد پرسنلی و رمز عبور را وارد کنید.", 400);

            CheckCredentialDto credential;
            try
            {
                // رمز دقیقاً همان‌طور که تایپ شده فرستاده می‌شود - نه trim، نه تبدیل ارقام.
                // فاصله یا رقم فارسی می‌تواند بخشی از رمزِ واقعی باشد.
                credential = await _userService.CheckUserAndPassword(new LoginRequestModel
                {
                    Username = username,
                    Password = password,
                });
            }
            catch (ExternalAuthException ex)
            {
                // کدِ ۵xx یعنی سامانه‌ی UM جواب درستی نداد - نه اینکه رمز غلط است.
                // یکی کردنِ این دو همان جایی است که کاربر رمزش را بی‌دلیل عوض می‌کند.
                return LoginFailure(StatusCodes.Status400BadRequest,
                                    ex.Code >= 500 ? "unavailable" : "invalid_credentials",
                                    string.IsNullOrWhiteSpace(ex.Message) ? "خطایی رخ داده است" : ex.Message,
                                    ex.Code);
            }

            // The UM service can return 200 OK with a null body, or with a body for an
            // account that is not active. Neither may be treated as a successful login.
            if (credential == null)
                return LoginFailure(StatusCodes.Status400BadRequest, "invalid_credentials",
                                    "نام کاربری یا رمز عبور نامعتبر است", 401);

            if (!credential.IsActive)
                return LoginFailure(StatusCodes.Status403Forbidden, "inactive",
                                    "حساب کاربری شما در سامانه‌ی مدیریت کاربران غیرفعال است.", 403);

            if (credential.PCode <= 0)
            {
                // توکن بدون کد پرسنلی یعنی کاربری بی‌هویت با نقشِ «فقط مشاهده». بهتر
                // است ورود رد شود و در لاگ دیده شود.
                _logger.LogError("سامانه‌ی مدیریت کاربران ورود را پذیرفت ولی کد پرسنلی معتبری برنگرداند؛ توکنی صادر نشد.");
                return LoginFailure(StatusCodes.Status400BadRequest, "unavailable", UnavailableMessage, 502);
            }

            var token = await _jwtService.GenerateJwtToken(credential.PCode.ToString(),
                                                           credential.FirstName, credential.LastName);

            // از پاسخ checkCredential فقط چیزی به مرورگر می‌رسد که صفحه لازم دارد. کد ملی
            // و موبایل عمداً اینجا نیستند: نمایش داده نمی‌شوند، و هر چه به مرورگر برسد
            // در حافظه، افزونه‌ها و ابزار توسعه‌دهنده در دسترس است.
            return Ok(new
            {
                Token = token,
                User = new
                {
                    credential.PCode,
                    credential.FirstName,
                    credential.LastName,
                    credential.IsFirstLogin,
                },
            });
        }

        // Development-only shortcut that mints a token for a made-up test identity by
        // role, with no call to the UM service or the database - so local frontend
        // work isn't blocked while the real UM host is unreachable from dev machines.
        // Disabled outside Development so it can never ship.
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

            const string devFirstName = "کاربر";
            const string devLastName = "آزمایشی";

            var token = await _jwtService.GenerateDevToken(pcode, model.Role, devFirstName, devLastName);
            return Ok(new
            {
                Token = token,
                User = new
                {
                    PCode = int.Parse(pcode, CultureInfo.InvariantCulture),
                    FirstName = devFirstName,
                    LastName = devLastName,
                    IsFirstLogin = false,
                },
            });
        }

        private ObjectResult LoginFailure(int status, string reason, string message, int code) =>
            StatusCode(status, new { message, code, reason });

        private static async Task<LoginRequestModel> ReadLoginRequestAsync(HttpRequest request)
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync();
                return new LoginRequestModel { Username = form["Username"], Password = form["Password"] };
            }

            if (request.ContentType != null &&
                request.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return await JsonSerializer.DeserializeAsync<LoginRequestModel>(request.Body, LoginJson)
                           ?? new LoginRequestModel();
                }
                catch (JsonException)
                {
                    return new LoginRequestModel();
                }
            }

            return new LoginRequestModel();
        }

        /// <summary>
        /// کد پرسنلی همان‌طور که کاربر تایپ یا پیست می‌کند به دست ما نمی‌رسد: صفحه‌کلید
        /// فارسی ارقام ۰-۹ فارسی یا عربی می‌سازد، و متنی که از یک سند فارسی کپی شده
        /// نویسه‌های نامرئیِ جهت (RLM/LRM) و نیم‌فاصله با خود می‌آورد. سامانه‌ی UM هیچ‌کدام
        /// را نمی‌شناسد و ورود با پیامِ «اطلاعات وارد شده صحیح نمی باشد» شکست می‌خورد -
        /// در حالی که کاربر دقیقاً کد درست را وارد کرده است.
        ///
        /// کلاینت هم همین کار را می‌کند؛ اینجا تکرار شده چون هر فراخواننده‌ای کلاینتِ ما نیست.
        /// </summary>
        private static string NormalizeUsername(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var normalized = new StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                if (ch >= '۰' && ch <= '۹')
                    normalized.Append((char)('0' + (ch - '۰')));
                else if (ch >= '٠' && ch <= '٩')
                    normalized.Append((char)('0' + (ch - '٠')));
                else if (char.IsWhiteSpace(ch) || CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format)
                    continue;
                else
                    normalized.Append(ch);
            }
            return normalized.ToString();
        }
    }
}
