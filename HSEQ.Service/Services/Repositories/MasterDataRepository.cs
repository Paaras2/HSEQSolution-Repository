using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Repositories
{
    public class MasterDataRepository : IMasterDataRepository
    {
        private readonly ApplicationDbContext _context;

        public MasterDataRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Project>> GetActiveProjectsAsync()
        {
            return await _context.Set<Project>()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Title)
                .ToListAsync();
        }

        public async Task<List<OrganizationalManagement>> GetActiveManagementsAsync()
        {
            return await _context.Set<OrganizationalManagement>()
                .Where(m => m.IsActive)
                .OrderBy(m => m.Title)
                .ToListAsync();
        }

        public async Task<List<OrganizationalActivity>> GetActiveActivitiesAsync()
        {
            return await _context.Set<OrganizationalActivity>()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Title)
                .ToListAsync();
        }

        public async Task<List<DocumentType>> GetActiveDocumentTypesAsync()
        {
            return await _context.Set<DocumentType>()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Title)
                .ToListAsync();
        }
    }
}
