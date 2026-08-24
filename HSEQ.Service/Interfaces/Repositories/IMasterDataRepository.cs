using HSEQ.Domain.Entities;

namespace HSEQ.Service.Interfaces.Repositories
{
    // Read-only master-data lookups needed to populate Document form dropdowns.
    // Kept separate from IDocumentNumberingRepository (which is part of the frozen
    // Document Numbering subsystem and only resolves single active records by id
    // for number generation) - this repository only ever lists active records
    // for display/selection.
    public interface IMasterDataRepository
    {
        Task<List<Project>> GetActiveProjectsAsync();
        Task<List<OrganizationalManagement>> GetActiveManagementsAsync();
        Task<List<OrganizationalActivity>> GetActiveActivitiesAsync();
        Task<List<DocumentType>> GetActiveDocumentTypesAsync();

        // ---- پنل ادمین ----
        // برخلاف متدهای بالا، این‌ها غیرفعال‌ها را هم برمی‌گردانند: پنل باید ردیف
        // غیرفعال را نشان دهد تا بشود دوباره فعالش کرد.
        Task<List<Project>> GetAllProjectsAsync();
        Task<List<OrganizationalManagement>> GetAllManagementsAsync();
        Task<List<OrganizationalActivity>> GetAllActivitiesAsync();
        Task<List<DocumentType>> GetAllDocumentTypesAsync();

        // جنریک، چون هر چهار نوع دقیقاً همین دو عملیات را لازم دارند و نوشتن هشت متد
        // تکراری فقط حجم می‌ساخت. به‌روزرسانی متدی ندارد چون EF تغییرِ موجودیتِ ردیابی‌شده
        // را خودش هنگام SaveChanges می‌نویسد.
        Task<T?> FindAsync<T>(Guid key) where T : class;
        Task AddAsync<T>(T entity) where T : class;

        // تعداد مدارک وابسته به هر ردیف - برای تشخیص اینکه حذف/غیرفعال‌سازی امن است یا نه.
        Task<Dictionary<Guid, int>> GetDocumentUsageAsync();

        // ---- آرشیو شماره‌های قدیمی ----
        // فقط خواندنی و صفحه‌بندی‌شده: ۷۶۸ ردیف است و برخلاف اطلاعات پایه، فرستادن کل
        // فهرست به مرورگر منطقی نیست. اینجا (و نه در یک ریپازیتوری جدا) قرار گرفته چون
        // همه‌ی تب‌های پنل ادمین از همین مسیر سرو می‌شوند.
        Task<(List<LegacyDocumentNumber> Items, int TotalCount)> GetLegacyDocumentNumbersAsync(
            string? query,
            int pageNumber,
            int pageSize);
    }
}
