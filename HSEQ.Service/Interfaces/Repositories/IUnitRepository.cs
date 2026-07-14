using HSEQ.API.Domain.Entities;

namespace HSEQ.API.Ripository
{
    //متدهایی که لازم دارم رو در اینجا فقط تعریف کردم
    //نتیجه متدها یک task است
    public interface IUnitRepository
    {
        Task<List<Unit>> GetAllAsync(bool includeDeactiveItems = true);
        Task<Unit?> GetByIdAsync(Guid id);
        Task AddAsync(Unit unit);
        Task UpdateAsync(Unit unit);
        Task DeleteAsync(Unit unit);
    }
}
