using HSEQ.API.Domain.Entities;
using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.API.Ripository;

namespace HSEQ.API.Service
{
    public class UnitService : IUnitService
    {
        private readonly IUnitRepository _unitRepository;

        public UnitService(IUnitRepository unitRepository)
        {
               _unitRepository = unitRepository;
        }
        //1:Add
        //Request Model با کار میکنه
        //IsActive = true هارکد شده
        //CreateUnitRequestModel از  به دلیل خط بالا در نتیجه BaseCreateRequestModel ارث بری نکرده است
        public async Task AddAsync(CreateUnitRequestModel request)
        {
            Unit unit = new Unit
            {
                Title = request.Title,
                IsActive = true
            };

            await _unitRepository.AddAsync(unit);
        }
        //2:Delete
        //با نتیجه متد دیگه کار میکنه
        public async Task DeleteAsync(Guid unitId)
        {
            var unit = await _unitRepository.GetByIdAsync(unitId);
            if (unit == null)
                throw new UnitNotFoundException();
            unit.IsActive = false;
            await _unitRepository.UpdateAsync(unit);

        }
        //3:GetAll
        //با UnitDto کار میشه
        public async Task<List<UnitDto>> GetAllAsync(bool includeInactiveItems = true)
        {
            var units = await _unitRepository.GetAllAsync(includeInactiveItems);

            return units.Select(unit => new UnitDto
            {
                Key = unit.Key,
                Title = unit.Title
            }).ToList();

        }
        //4:GetById
        //با UnitDto کار میشه
        public async Task<UnitDto> GetByIdAsync(Guid id)
        {
            var unit = await _unitRepository.GetByIdAsync(id);

            if (unit == null)
                throw new UnitNotFoundException();

            return new UnitDto
            {
                Key = unit.Key,
                Title = unit.Title,
                IsActive = unit.IsActive,
                CreatedTime = unit.CreatedTime,
                ModifiedDate = unit.ModifiedDate
            };
        }
        //5:Update
        //Request Model با کار میکنه
        public async Task UpdateAsync(UpdateUnitRequestModel request)
        {
            var unit = await _unitRepository.GetByIdAsync(request.Key);

            if (unit == null)
                throw new UnitNotFoundException();

            unit.Title = request.Title;
            unit.IsActive = request.IsActive;

            await _unitRepository.UpdateAsync(unit);
        }
    }
}
