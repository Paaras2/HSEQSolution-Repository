using HSEQ.API.Domain;
using HSEQ.Common;
using HSEQ.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDomainLayerServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDBContext>(options =>
            {
                options
                .UseSqlServer(AppSettingFactory.AppSetting.SqlConnection);

            });

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            return services;
        }
    }
}
