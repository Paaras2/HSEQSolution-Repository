
using HSEQ.Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace HSEQ.Service.Services.Repositories
{
    public class Repository<T> where T : class

    {
        private readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<List<T>> GetAllAsync(bool includeDeactiveItems = true)
        {
            if (includeDeactiveItems)
            {
                return await _dbSet.ToListAsync();
            }
            return await _dbSet.Where(e => EF.Property<bool>(e, "IsActive")).ToListAsync();
        }

        public IQueryable<T> GetAllAsQueryable()
        {
            return _dbSet.AsQueryable();
        }

        public async Task<List<T>> GetAllAsync(int pageNumber, int pageSize)
        {
            return await _dbSet
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<T>> GetAllWithIncludesAsync(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return await query.ToListAsync();
        }

        public IQueryable<T> GetAllWithIncludes(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return query;
        }

        public IQueryable<T> GetAllWithComplexIncludes(Func<IQueryable<T>, IQueryable<T>> includeFunc)
        {
            return includeFunc(_dbSet.AsQueryable());
        }

        public async Task<T?> GetByIdAsync(Guid id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T?> GetByIdWithIncludesAsync(Guid id, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return await query.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "key") == id);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(List<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public virtual async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);

        }
        public async Task UpdateRangeAsync(List<T> entities)
        {
            _dbSet.UpdateRange(entities);

        }
        public async Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
        }



        



    }
}