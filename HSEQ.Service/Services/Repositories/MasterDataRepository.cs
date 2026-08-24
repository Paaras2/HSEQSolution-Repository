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

        // ---- پنل ادمین: شامل غیرفعال‌ها ----
        public async Task<List<Project>> GetAllProjectsAsync() =>
            await _context.Set<Project>().OrderBy(p => p.Code).ToListAsync();

        public async Task<List<OrganizationalManagement>> GetAllManagementsAsync() =>
            await _context.Set<OrganizationalManagement>().OrderBy(m => m.Code).ToListAsync();

        public async Task<List<OrganizationalActivity>> GetAllActivitiesAsync() =>
            await _context.Set<OrganizationalActivity>().OrderBy(a => a.Code).ToListAsync();

        public async Task<List<DocumentType>> GetAllDocumentTypesAsync() =>
            await _context.Set<DocumentType>().OrderBy(t => t.Code).ToListAsync();

        public async Task<T?> FindAsync<T>(Guid key) where T : class =>
            await _context.Set<T>().FindAsync(key);

        public async Task AddAsync<T>(T entity) where T : class =>
            await _context.Set<T>().AddAsync(entity);

        // چهار شمارش در یک رفت‌وبرگشت. کلید نتیجه، کلیدِ خودِ ردیف اطلاعات پایه است،
        // پس یک دیکشنری برای هر چهار نوع کافی است (کلیدها Guid و یکتا هستند).
        public async Task<Dictionary<Guid, int>> GetDocumentUsageAsync()
        {
            var usage = new Dictionary<Guid, int>();

            void Merge(List<KeyValuePair<Guid, int>> rows)
            {
                foreach (var row in rows)
                    usage[row.Key] = usage.TryGetValue(row.Key, out var current) ? current + row.Value : row.Value;
            }

            var documents = _context.Set<Document>();

            Merge(await documents.Where(d => d.ProjectId != null)
                .GroupBy(d => d.ProjectId!.Value)
                .Select(g => new KeyValuePair<Guid, int>(g.Key, g.Count()))
                .ToListAsync());

            Merge(await documents
                .GroupBy(d => d.OrganizationalManagementId)
                .Select(g => new KeyValuePair<Guid, int>(g.Key, g.Count()))
                .ToListAsync());

            Merge(await documents
                .GroupBy(d => d.OrganizationalActivityId)
                .Select(g => new KeyValuePair<Guid, int>(g.Key, g.Count()))
                .ToListAsync());

            Merge(await documents
                .GroupBy(d => d.DocumentTypeId)
                .Select(g => new KeyValuePair<Guid, int>(g.Key, g.Count()))
                .ToListAsync());

            return usage;
        }

        // ---- آرشیو شماره‌های قدیمی ----
        // فیلتر و صفحه‌بندی هر دو در دیتابیس انجام می‌شوند. ترتیب بر اساس شماره‌ی خام است،
        // چون آرشیو کد ۵حرفیِ هم‌گروه را کنار هم نگه می‌دارد و همان ترتیب برای مرور
        // طبیعی‌ترین است. ردیف‌های بدون کد (چهار مورد استثنایی) هم در همین ترتیب می‌آیند.
        public async Task<(List<LegacyDocumentNumber> Items, int TotalCount)> GetLegacyDocumentNumbersAsync(
            string? query,
            int pageNumber,
            int pageSize)
        {
            var source = _context.Set<LegacyDocumentNumber>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                source = source.Where(l =>
                    l.RawNumber.Contains(term) ||
                    (l.Code5 != null && l.Code5.Contains(term)) ||
                    (l.Name != null && l.Name.Contains(term)) ||
                    (l.UnitLabel != null && l.UnitLabel.Contains(term)));
            }

            var totalCount = await source.CountAsync();

            var items = await source
                .OrderBy(l => l.RawNumber)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
