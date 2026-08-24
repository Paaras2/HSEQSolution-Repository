using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;

namespace HSEQ.Service.Interfaces.Services
{
    public interface IMasterDataService
    {
        Task<List<ProjectLookupDto>> GetProjectsAsync();
        Task<List<OrganizationalManagementLookupDto>> GetOrganizationalManagementsAsync();
        Task<List<OrganizationalActivityLookupDto>> GetOrganizationalActivitiesAsync();
        Task<List<DocumentTypeLookupDto>> GetDocumentTypesAsync();

        // ---- پنل ادمین ----
        // فهرست کامل (شامل غیرفعال‌ها) به‌همراه تعداد مدارک وابسته به هر ردیف.
        Task<List<MasterDataItemDto>> GetAllForAdminAsync(MasterDataKind kind);

        Task CreateAsync(CreateMasterDataRequestModel request);

        // کد قابل ویرایش نیست؛ فقط عنوان و وضعیت. دلیلش در UpdateMasterDataRequestModel آمده.
        Task UpdateAsync(UpdateMasterDataRequestModel request);

        // آرشیو شماره‌های قدیمی - فقط خواندنی، با جستجو و صفحه‌بندی سمت سرور.
        Task<LegacyDocumentNumberPagedResult> GetLegacyDocumentNumbersAsync(
            LegacyDocumentNumberQueryRequestModel request);
    }
}
