using HSEQ.API.Domain;
using HSEQ.API.Domain.Entities;

namespace HSEQ.API.Ripository
{
    public class UnitRepository : Repository<Unit>, IUnitRepository
    {
        private readonly ApplicationDbContext _context;
        public UnitRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

       
    }
}
