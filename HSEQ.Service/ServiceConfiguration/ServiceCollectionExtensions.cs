using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using HSEQ.Service.Services.Repositories;
using HSEQ.Service.Services.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Service
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceLayerServices(this IServiceCollection services)
        {
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<ISeedDatabase, SeedDatabase>();
            // ورود یک‌باره‌ی اسناد سامانه‌ی قدیمی؛ فقط از خط فرمان صدا زده می‌شود.
            services.AddScoped<ILegacyDocumentImportService, LegacyDocumentImportService>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IDocumentNumberingRepository, DocumentNumberingRepository>();
            services.AddScoped<IDocumentNumberGeneratorService, DocumentNumberGeneratorService>();
            services.AddScoped<IMasterDataRepository, MasterDataRepository>();
            services.AddScoped<IMasterDataService, MasterDataService>();
            services.AddScoped<IFileTextExtractionService, FileTextExtractionService>();
            // بازسازی متن فایلِ اسناد موجود - هم از پنل ادمین و هم از خط فرمان.
            services.AddScoped<IDocumentTextIndexService, DocumentTextIndexService>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IDocumentRelationRepository, DocumentRelationRepository>();
            services.AddScoped<IDocumentRelationService, DocumentRelationService>();
            return services;
        }
    }
}
