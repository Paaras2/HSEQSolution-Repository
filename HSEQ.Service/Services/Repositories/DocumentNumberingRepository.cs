using HSEQ.Domain;
using HSEQ.Domain.DocumentNumbering;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Repositories
{
    public class DocumentNumberingRepository : IDocumentNumberingRepository
    {
        private readonly ApplicationDbContext _context;

        public DocumentNumberingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Project?> GetActiveProjectAsync(Guid projectId)
        {
            return await _context.Set<Project>()
                .FirstOrDefaultAsync(p => p.Key == projectId && p.IsActive);
        }

        public async Task<OrganizationalManagement?> GetActiveManagementAsync(Guid managementId)
        {
            return await _context.Set<OrganizationalManagement>()
                .FirstOrDefaultAsync(m => m.Key == managementId && m.IsActive);
        }

        public async Task<OrganizationalActivity?> GetActiveActivityAsync(Guid activityId, Guid managementId)
        {
            return await _context.Set<OrganizationalActivity>()
                .FirstOrDefaultAsync(a => a.Key == activityId
                                        && a.OrganizationalManagementId == managementId
                                        && a.IsActive);
        }

        public async Task<DocumentType?> GetActiveDocumentTypeAsync(Guid documentTypeId)
        {
            return await _context.Set<DocumentType>()
                .FirstOrDefaultAsync(t => t.Key == documentTypeId && t.IsActive);
        }

        public async Task<int> AllocateNextSerialNumberAsync()
        {
            // NEXT VALUE FOR requires a literal object name - it cannot be parameterized.
            // DocumentSerialSequenceInfo.QualifiedName is a compile-time constant, not user
            // input, so baking it into the SQL text here is safe.
            FormattableString sql = FormattableStringFactory.Create(
                $"SELECT NEXT VALUE FOR {DocumentSerialSequenceInfo.QualifiedName} AS Value");

            // FirstAsync()/SingleAsync() make EF Core wrap this in an outer
            // "SELECT TOP(1) ... FROM (<sql>) AS [t]" - SQL Server rejects NEXT VALUE FOR
            // inside any derived table/subquery (error 11719). ToListAsync() executes the
            // raw SQL as-is with no wrapping, so take the single row client-side instead.
            var results = await _context.Database.SqlQuery<int>(sql).ToListAsync();
            return results[0];
        }

        public async Task<bool> NumberExistsAsync(string number)
        {
            return await _context.Set<Document>().AnyAsync(d => d.Number == number);
        }
    }
}