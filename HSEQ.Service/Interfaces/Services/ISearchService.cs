using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;

namespace HSEQ.Service.Interfaces.Services
{
    public interface ISearchService
    {
        Task<PagedResult> SearchAsync(SearchDocumentsRequestModel request);

        // پیشنهاد خودکار هنگام تایپ - فقط شماره/نام، بدون فیلترهای پیشرفته.
        Task<List<DocumentSuggestionDto>> SuggestAsync(string query);

        // همان فیلترهای SearchAsync، بدون صفحه‌بندی (تا سقف داخلی)، به‌صورت بایت‌های .xlsx.
        Task<byte[]> ExportAsync(SearchDocumentsRequestModel request);
    }
}
