using HSEQ.API.Ripository;
using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Repositories
{
    public class DocumentRepository : Repository<Document>, IDocumentRepository
    {
        private readonly ApplicationDBContext _context;
        public DocumentRepository(ApplicationDBContext context) : base(context)
        {
            _context = context;
        }
    }
}
