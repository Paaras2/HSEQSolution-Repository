using HSEQ.API.Model.Dtos;
using HSEQ.API.Model.RequestModels;
using HSEQ.Common;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using System.Text.RegularExpressions;

namespace HSEQ.Service.Services.Services
{
    public class MasterDataService : IMasterDataService
    {
        private readonly IMasterDataRepository _masterDataRepository;

        public MasterDataService(IMasterDataRepository masterDataRepository)
        {
            _masterDataRepository = masterDataRepository;
        }

        public async Task<List<ProjectLookupDto>> GetProjectsAsync()
        {
            var projects = await _masterDataRepository.GetActiveProjectsAsync();
            return projects.Select(p => new ProjectLookupDto
            {
                Key = p.Key,
                Code = p.Code,
                Title = p.Title,
                IsProjectRelated = p.IsProjectRelated
            }).ToList();
        }

        public async Task<List<OrganizationalManagementLookupDto>> GetOrganizationalManagementsAsync()
        {
            var managements = await _masterDataRepository.GetActiveManagementsAsync();
            return managements.Select(m => new OrganizationalManagementLookupDto
            {
                Key = m.Key,
                Code = m.Code,
                Title = m.Title
            }).ToList();
        }

        public async Task<List<OrganizationalActivityLookupDto>> GetOrganizationalActivitiesAsync()
        {
            var activities = await _masterDataRepository.GetActiveActivitiesAsync();
            return activities.Select(a => new OrganizationalActivityLookupDto
            {
                Key = a.Key,
                Code = a.Code,
                Title = a.Title,
                OrganizationalManagementId = a.OrganizationalManagementId
            }).ToList();
        }

        public async Task<List<DocumentTypeLookupDto>> GetDocumentTypesAsync()
        {
            var types = await _masterDataRepository.GetActiveDocumentTypesAsync();
            return types.Select(t => new DocumentTypeLookupDto
            {
                Key = t.Key,
                Code = t.Code,
                Title = t.Title
            }).ToList();
        }
        // ---- پنل ادمین ----

        // طول کد هر نوع، دقیقاً همان چیزی که ساختار شماره‌ی مدرک بر آن بنا شده و در
        // پیکربندی EF هم اعمال شده است. تغییر این عددها یعنی شکستن شماره‌گذاری.
        private static int CodeLengthFor(MasterDataKind kind) => kind switch
        {
            MasterDataKind.Project => 4,
            MasterDataKind.OrganizationalManagement => 1,
            MasterDataKind.OrganizationalActivity => 2,
            MasterDataKind.DocumentType => 2,
            _ => 2
        };

        // فقط حروف انگلیسی و رقم: کد داخل شماره‌ی مدرک می‌نشیند و باید در نام فایل،
        // آدرس و خروجی اکسل هم بی‌دردسر باشد.
        private static readonly Regex CodePattern = new("^[A-Za-z0-9]+$", RegexOptions.Compiled);

        public async Task<List<MasterDataItemDto>> GetAllForAdminAsync(MasterDataKind kind)
        {
            var usage = await _masterDataRepository.GetDocumentUsageAsync();

            int Used(Guid key) => usage.TryGetValue(key, out var count) ? count : 0;

            switch (kind)
            {
                case MasterDataKind.Project:
                    var projects = await _masterDataRepository.GetAllProjectsAsync();
                    return projects.Select(x => new MasterDataItemDto
                    {
                        Key = x.Key,
                        Code = x.Code,
                        Title = x.Title,
                        IsActive = x.IsActive,
                        IsProjectRelated = x.IsProjectRelated,
                        UsageCount = Used(x.Key)
                    }).ToList();

                case MasterDataKind.OrganizationalManagement:
                    var managements = await _masterDataRepository.GetAllManagementsAsync();
                    return managements.Select(x => new MasterDataItemDto
                    {
                        Key = x.Key,
                        Code = x.Code,
                        Title = x.Title,
                        IsActive = x.IsActive,
                        UsageCount = Used(x.Key)
                    }).ToList();

                case MasterDataKind.OrganizationalActivity:
                    var activities = await _masterDataRepository.GetAllActivitiesAsync();
                    return activities.Select(x => new MasterDataItemDto
                    {
                        Key = x.Key,
                        Code = x.Code,
                        Title = x.Title,
                        IsActive = x.IsActive,
                        OrganizationalManagementId = x.OrganizationalManagementId,
                        UsageCount = Used(x.Key)
                    }).ToList();

                default:
                    var types = await _masterDataRepository.GetAllDocumentTypesAsync();
                    return types.Select(x => new MasterDataItemDto
                    {
                        Key = x.Key,
                        Code = x.Code,
                        Title = x.Title,
                        IsActive = x.IsActive,
                        UsageCount = Used(x.Key)
                    }).ToList();
            }
        }

        public async Task CreateAsync(CreateMasterDataRequestModel request)
        {
            var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
            var title = (request.Title ?? string.Empty).Trim();

            var expectedLength = CodeLengthFor(request.Kind);
            if (code.Length != expectedLength || !CodePattern.IsMatch(code))
                throw new InvalidMasterDataCodeException(expectedLength);

            if (string.IsNullOrWhiteSpace(title))
                throw new MasterDataTitleRequiredException();

            // یکتایی پیش از درج بررسی می‌شود تا کاربر پیام فارسی بگیرد، نه خطای
            // ایندکس یکتای دیتابیس. ایندکس همچنان لایه‌ی دوم دفاع است.
            await EnsureCodeIsFreeAsync(request.Kind, code, request.OrganizationalManagementId);

            switch (request.Kind)
            {
                case MasterDataKind.Project:
                    await _masterDataRepository.AddAsync(new Project
                    {
                        Code = code,
                        Title = title,
                        IsProjectRelated = request.IsProjectRelated,
                        IsActive = true,
                        CreatedTime = DateTime.Now
                    });
                    break;

                case MasterDataKind.OrganizationalManagement:
                    await _masterDataRepository.AddAsync(new OrganizationalManagement
                    {
                        Code = code,
                        Title = title,
                        IsActive = true,
                        CreatedTime = DateTime.Now
                    });
                    break;

                case MasterDataKind.OrganizationalActivity:
                    if (request.OrganizationalManagementId is null)
                        throw new ManagementRequiredForActivityException();

                    // مدیریت والد باید واقعاً وجود داشته باشد، وگرنه FK با خطای مبهم می‌شکست.
                    var parent = await _masterDataRepository.FindAsync<OrganizationalManagement>(
                        request.OrganizationalManagementId.Value);
                    if (parent is null)
                        throw new OrganizationalManagementNotFoundException();

                    await _masterDataRepository.AddAsync(new OrganizationalActivity
                    {
                        Code = code,
                        Title = title,
                        OrganizationalManagementId = request.OrganizationalManagementId.Value,
                        IsActive = true,
                        CreatedTime = DateTime.Now
                    });
                    break;

                default:
                    await _masterDataRepository.AddAsync(new DocumentType
                    {
                        Code = code,
                        Title = title,
                        IsActive = true,
                        CreatedTime = DateTime.Now
                    });
                    break;
            }
        }

        public async Task UpdateAsync(UpdateMasterDataRequestModel request)
        {
            var title = (request.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
                throw new MasterDataTitleRequiredException();

            // غیرفعال‌کردن ردیفی که مدرکی به آن وابسته است، فهرست‌های موجود را می‌شکند.
            if (!request.IsActive)
            {
                var usage = await _masterDataRepository.GetDocumentUsageAsync();
                if (usage.TryGetValue(request.Key, out var usedBy) && usedBy > 0)
                    throw new MasterDataItemInUseException(usedBy);
            }

            switch (request.Kind)
            {
                case MasterDataKind.Project:
                    var project = await _masterDataRepository.FindAsync<Project>(request.Key)
                        ?? throw new MasterDataItemNotFoundException();
                    project.Title = title;
                    project.IsActive = request.IsActive;
                    project.IsProjectRelated = request.IsProjectRelated;
                    project.ModifiedDate = DateTime.Now;
                    break;

                case MasterDataKind.OrganizationalManagement:
                    var management = await _masterDataRepository.FindAsync<OrganizationalManagement>(request.Key)
                        ?? throw new MasterDataItemNotFoundException();
                    management.Title = title;
                    management.IsActive = request.IsActive;
                    management.ModifiedDate = DateTime.Now;
                    break;

                case MasterDataKind.OrganizationalActivity:
                    var activity = await _masterDataRepository.FindAsync<OrganizationalActivity>(request.Key)
                        ?? throw new MasterDataItemNotFoundException();
                    activity.Title = title;
                    activity.IsActive = request.IsActive;
                    activity.ModifiedDate = DateTime.Now;
                    break;

                default:
                    var documentType = await _masterDataRepository.FindAsync<DocumentType>(request.Key)
                        ?? throw new MasterDataItemNotFoundException();
                    documentType.Title = title;
                    documentType.IsActive = request.IsActive;
                    documentType.ModifiedDate = DateTime.Now;
                    break;
            }

            // موجودیت‌ها ردیابی‌شده‌اند؛ نوشتن با SaveChanges در UnitOfWork انجام می‌شود.
        }

        // فعالیت سازمانی فقط داخل مدیریت والدش یکتاست (ایندکس مرکب)، بقیه سراسری.
        private async Task EnsureCodeIsFreeAsync(MasterDataKind kind, string code, Guid? managementId)
        {
            bool taken = kind switch
            {
                MasterDataKind.Project =>
                    (await _masterDataRepository.GetAllProjectsAsync()).Any(x => x.Code == code),
                MasterDataKind.OrganizationalManagement =>
                    (await _masterDataRepository.GetAllManagementsAsync()).Any(x => x.Code == code),
                MasterDataKind.OrganizationalActivity =>
                    (await _masterDataRepository.GetAllActivitiesAsync())
                        .Any(x => x.Code == code && x.OrganizationalManagementId == managementId),
                _ => (await _masterDataRepository.GetAllDocumentTypesAsync()).Any(x => x.Code == code)
            };

            if (taken)
                throw new DuplicateMasterDataCodeException(code);
        }

        // آرشیو شماره‌های قدیمی. هیچ عملیات نوشتنی ندارد: جدول یک‌بار از اکسل وارد شده و
        // مرجع تاریخی است، پس تب مربوطه هم فقط خواندنی است.
        public async Task<LegacyDocumentNumberPagedResult> GetLegacyDocumentNumbersAsync(
            LegacyDocumentNumberQueryRequestModel request)
        {
            // مقادیر بیرون از محدوده‌ی منطقی اصلاح می‌شوند تا یک درخواست دستکاری‌شده
            // نتواند کل جدول را یکجا بکشد.
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

            var (items, totalCount) = await _masterDataRepository.GetLegacyDocumentNumbersAsync(
                request.Query, pageNumber, pageSize);

            return new LegacyDocumentNumberPagedResult
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items.Select(l => new LegacyDocumentNumberDto
                {
                    Key = l.Key,
                    RawNumber = l.RawNumber,
                    Code5 = l.Code5,
                    SerialNumber = l.SerialNumber,
                    RevisionSuffix = l.RevisionSuffix,
                    ManagementCode = l.ManagementCode,
                    ActivityCode = l.ActivityCode,
                    DocumentTypeCode = l.DocumentTypeCode,
                    Name = l.Name,
                    UnitLabel = l.UnitLabel,
                    LastEditShamsiDate = l.LastEditShamsiDate,
                    CurrentEditShamsiDate = l.CurrentEditShamsiDate,
                    CurrentVersion = l.CurrentVersion,
                }).ToList(),
            };
        }
    }
}
