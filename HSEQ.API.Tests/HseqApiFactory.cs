using HSEQ.Service.Interfaces.Services;
using HSEQ.Shared.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HSEQ.API.Tests;

/// <summary>
/// برنامه‌ی واقعی ‎HSEQ.API‎ را بالا می‌آورد - همان ‎Program.cs‎، همان pipeline، همان
/// کنترلرها - و فقط دو وابستگیِ بیرونی را جایگزین می‌کند: سرویس UM و جدول نقش‌ها.
///
/// نکته‌ی مهم: چیدمان میزبانی پارامتر است، نه فرض. یک نمونه برای Kestrel در ریشه و
/// یکی برای IIS به‌عنوان Application زیر ‎/api‎ ساخته می‌شود، و هر دو باید دقیقاً یک
/// قرارداد عمومی بدهند.
/// </summary>
public sealed class HseqApiFactory : WebApplicationFactory<Program>
{
    private readonly HostingModel _hostingModel;
    private readonly string _environmentName;

    /// <summary>مسیر Application در حالت <see cref="HostingModel.IisChildApplicationAtOtherPath"/>.</summary>
    public const string AlternateVirtualPath = "/hseq-backend";

    public HseqApiFactory(HostingModel hostingModel, string? environmentName = null)
    {
        _hostingModel = hostingModel;
        _environmentName = environmentName ?? Environments.Development;
    }

    /// <summary>پیشوندی که مرورگر در این چیدمان صدا می‌زند.</summary>
    public string PublicPrefix => _hostingModel switch
    {
        HostingModel.IisChildApplicationAtOtherPath => AlternateVirtualPath,
        _ => "/api",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUMService>();
            services.AddScoped<IUMService, StubUmService>();

            services.RemoveAll<IAdminService>();
            services.AddScoped<IAdminService, StubAdminService>();

            services.AddSingleton<RequestPathRecorder>();
            services.Configure<MvcOptions>(options => options.Filters.Add<RequestPathRecordingFilter>());

            // شبیه‌سازی IIS: پیش از هر میان‌افزاری که Program.cs ثبت کرده، پیشوند
            // Application از مسیر برداشته و به PathBase منتقل می‌شود.
            if (_hostingModel is HostingModel.IisChildApplicationAtApi or HostingModel.IisChildApplicationAtOtherPath)
            {
                var virtualPath = _hostingModel == HostingModel.IisChildApplicationAtApi ? "/api" : AlternateVirtualPath;
                services.AddSingleton<IStartupFilter>(new IisChildApplicationEmulator(virtualPath));
            }
        });
    }
}
