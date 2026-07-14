using HSEQ.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Repositories
{
    public interface IDocumentRepository
    {
        Task<List<Document>> GetAllAsync(bool includeDeactiveItems = true);
        Task<Document?> GetByIdAsync(Guid id);
        Task AddAsync(Document document);
        Task UpdateAsync(Document document);
        Task<Document?> GetByIdWithIncludesAsync(Guid id, params Expression<Func<Document, object>>[] includes);
        Task<List<Document>> GetAllWithIncludesAsync(params Expression<Func<Document, object>>[] includes);
        IQueryable<Document> GetAllAsQueryable();
        Task DeleteAsync(Document entity);

    }
}
