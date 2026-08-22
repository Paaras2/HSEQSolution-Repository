using HSEQ.API.Model.Dtos;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetSummaryAsync();
    }
}
