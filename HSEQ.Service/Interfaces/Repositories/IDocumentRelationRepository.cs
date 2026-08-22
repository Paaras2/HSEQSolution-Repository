using HSEQ.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HSEQ.Service.Interfaces.Repositories
{
    public interface IDocumentRelationRepository
    {
        Task<DocumentRelation?> GetByIdAsync(Guid id);
        Task AddAsync(DocumentRelation relation);
        Task AddRangeAsync(List<DocumentRelation> relations);
        Task UpdateRangeAsync(List<DocumentRelation> relations);
        Task DeleteAsync(DocumentRelation relation);
        IQueryable<DocumentRelation> GetAllAsQueryable();
    }
}
