using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;

namespace HSEQ.Service.Services.Repositories
{
    public class DocumentRelationRepository : Repository<DocumentRelation>, IDocumentRelationRepository
    {
        public DocumentRelationRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
