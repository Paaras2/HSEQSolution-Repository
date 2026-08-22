using HSEQ.Domain;
using HSEQ.Domain.DocumentNumbering;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HSEQ.Service.Services.Repositories
{
    public class DocumentNumberingRepository : IDocumentNumberingRepository
    {
        private const string HeadquartersCodeCounterTable = "dbo.HeadquartersCodeCounters";

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

        public async Task<int> AllocateNextHeadquartersSerialAsync(string code5)
        {
            // Table name is concatenated into the *format* string (plain string
            // concatenation, evaluated before any interpolation happens), and only
            // code5 is left as the "{0}" hole - so FormattableStringFactory.Create
            // parameterizes code5 alone and never touches the table identifier. Doing it
            // this way (rather than a single "$..." with both in it) removes any
            // ambiguity about which of the two would end up as a SQL parameter.
            var updateFormat = "UPDATE " + HeadquartersCodeCounterTable +
                " SET LastSerialNumber = LastSerialNumber + 1, ModifiedDate = GETUTCDATE()" +
                " OUTPUT INSERTED.LastSerialNumber WHERE Code5 = {0}";
            var updateSql = FormattableStringFactory.Create(updateFormat, code5);

            // Single atomic UPDATE...OUTPUT avoids a read-then-write race between two
            // requests allocating a serial for the same 5-letter code at once - same
            // reasoning as AllocateNextSerialNumberAsync above, just per-code instead of
            // global. ToListAsync() (not FirstAsync/SingleAsync) so EF Core executes this
            // as-is instead of wrapping it in a derived table.
            var updated = await _context.Database.SqlQuery<int>(updateSql).ToListAsync();
            if (updated.Count > 0)
                return updated[0];

            // First time this exact code has ever been used for a Headquarters document -
            // no legacy row and no prior allocation seeded a counter for it yet.
            try
            {
                // [Key] باید براکت داشته باشد: KEY در T-SQL کلمه‌ی رزرو است و بدون براکت
                // این INSERT خطای نحوی می‌دهد. آن خطا را catch پایین می‌گرفت، UPDATE را دوباره
                // اجرا می‌کرد، صفر ردیف می‌گرفت و روی retried[0] می‌ترکید - یعنی هر سند ستادیِ
                // با ترکیب مدیریت+فعالیت+نوعِ تازه، با 500 شکست می‌خورد.
                var insertFormat = "INSERT INTO " + HeadquartersCodeCounterTable +
                    " ([Key], Code5, LastSerialNumber, IsActive, CreatedTime) VALUES (NEWID(), {0}, 1, 1, GETUTCDATE())";
                await _context.Database.ExecuteSqlInterpolatedAsync(FormattableStringFactory.Create(insertFormat, code5));
                return 1;
            }
            catch (SqlException)
            {
                // Lost the race to insert - another request created the row first, so its
                // UPDATE succeeds now.
                var retried = await _context.Database.SqlQuery<int>(updateSql).ToListAsync();
                return retried[0];
            }
        }
    }
}