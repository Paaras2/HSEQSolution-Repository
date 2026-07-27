using HSEQ.API.Ripository;
using HSEQ.API.Service;
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
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IUnitRepository, UnitRepository>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IUnitService, UnitService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IAdminService, AdminService>();
            return services;
        }
    }
}
