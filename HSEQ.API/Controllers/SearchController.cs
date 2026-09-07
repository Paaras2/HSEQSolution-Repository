using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Service.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSEQ.API.Controllers
{
    // جستجوی پیشرفته - فقط خواندنی، پس مثل بقیه‌ی مسیرهای خواندنی سند برای هر کاربر
    // احرازهویت‌شده باز است (نه فقط Admin/DocumentManager).
    [Route("api/[controller]")]
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

        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] SearchDocumentsRequestModel request)
        {
            var bytes = await _searchService.ExportAsync(request);
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(bytes, contentType, "documents-export.xlsx");
        }
    }
}
