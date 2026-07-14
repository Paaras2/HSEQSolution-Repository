


using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;

namespace HSEQ.API.Service
{
    public interface IUnitService
    {
        Task AddAsync(CreateUnitRequestModel request);

        Task UpdateAsync(UpdateUnitRequestModel request);

        Task DeleteAsync(Guid unitId);

        Task<UnitDto> GetByIdAsync(Guid id);

        Task<List<UnitDto>> GetAllAsync(bool includeInactiveItems = true);
    }
}
