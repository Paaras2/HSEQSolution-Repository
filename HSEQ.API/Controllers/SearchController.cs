using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    // جستجوی پیشرفته - فقط خواندنی، پس مثل بقیه‌ی مسیرهای خواندنی سند برای هر کاربر
    // احرازهویت‌شده باز است (نه فقط Admin/DocumentManager). استثنا خروجی اکسل است:
    // آن یکی و فهرستِ ستون‌هایش فقط Admin و DocumentManager - پایین را ببینید.
    [Route("[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("documents")]
        public async Task<PagedResult> SearchDocuments([FromQuery] SearchDocumentsRequestModel request)
        {
            var result = await _searchService.SearchAsync(request);
            DocumentDtoVisibility.ApplyOutsideCodingVisibility(result.Items, User);
            return result;
        }

        [HttpGet("suggestions")]
        public async Task<List<DocumentSuggestionDto>> Suggestions([FromQuery] string query)
        {
            return await _searchService.SuggestAsync(query);
        }

        // خروجی اکسل شاملِ داده‌ی خامِ کلِ آرشیو است (ازجمله اسنادی که کاربرِ فقط‌مشاهده
        // در فهرستِ عادی نمی‌بیند)، پس محدود به همان دو نقشی شد که سند را مدیریت
        // می‌کنند (Admin و DocumentManager) - نه یک تصمیمِ رابطِ کاربری، اجباریِ سمتِ
        // سرور که با Roles اعمال می‌شود.
        [Authorize(Roles = "Admin,DocumentManager")]
        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] SearchDocumentsRequestModel request)
        {
            var bytes = await _searchService.ExportAsync(request);
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(bytes, contentType, "documents-export.xlsx");
        }

        // فهرستِ ستون‌های قابل‌انتخاب برای همان خروجی - رابطِ کاربری منوی سفارشی‌سازی
        // را از همین می‌سازد تا کلید و برچسبِ هر ستون فقط یک‌جا (HSEQ.Common) تعریف شده باشد.
        [Authorize(Roles = "Admin,DocumentManager")]
        [HttpGet("export/columns")]
        public ActionResult<List<ExportColumnDto>> ExportColumns()
        {
            return DocumentExportColumns.All
                .Select(c => new ExportColumnDto
                {
                    Key = c.Key,
                    Label = c.Label,
                    IsDefault = DocumentExportColumns.Default.Contains(c.Key),
                })
                .ToList();
        }
    }
}
