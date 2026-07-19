using HSEQ.Domain.Entities;
using System;

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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
        //includes abzari baraye join zadan ast
        Task<Document?> GetByIdWithIncludesAsync(Guid id, params Expression<Func<Document, object>>[] includes);
        //includes abzari baraye join zadan ast
        Task<List<Document>> GetAllWithIncludesAsync(params Expression<Func<Document, object>>[] includes);
        //jozce linq ast
        IQueryable<Document> GetAllAsQueryable();
        Task DeleteAsync(Document entity);

    }
}
