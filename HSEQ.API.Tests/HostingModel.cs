using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HSEQ.API.Tests;

/// <summary>
/// چیدمان‌های میزبانی‌ای که برنامه واقعاً در آن‌ها اجرا می‌شود. قرارداد عمومی
/// (‎/api/Auth/login‎) باید در هر دو یکسان باشد.
/// </summary>
public enum HostingModel
{
    /// <summary>
    /// Kestrel در ریشه: ‎dotnet run‎ در توسعه و ‎dotnet HSEQ.API.dll‎ روی خروجی publish.
    /// هیچ میزبانی پیشوند را برنمی‌دارد؛ برنامه خودش PathBase را اعمال می‌کند.
    /// </summary>
    KestrelAtRoot,

    /// <summary>
    /// IIS، بک‌اند به‌عنوان Application با مسیر ‎/api‎ زیر سایت کلاینت - همان چیزی که
    /// روی سرور ثبت شده است. IIS پیشوند را برمی‌دارد و ‎PathBase=/api‎ می‌گذارد.
    /// </summary>
    IisChildApplicationAtApi,

    /// <summary>
    /// همان چیدمان IIS ولی با مسیر دیگری. هیچ‌جای کد نباید مقدار ‎/api‎ را فرض کند؛
    /// این حالت ثابت می‌کند مالکِ پیشوند واقعاً میزبان است، نه کد.
    /// </summary>
    IisChildApplicationAtOtherPath,
}

/// <summary>
/// شبیه‌سازی همان کاری که ماژول ASP.NET Core در IIS با درخواست می‌کند: پیشوندِ
/// Application را از مسیر برمی‌دارد و به‌عنوان PathBase اعلام می‌کند.
///
/// از IStartupFilter استفاده می‌شود چون میان‌افزارِ آن *پیش از* هر چیزی که در
/// ‎Program.cs‎ روی ‎app‎ ثبت شده اجرا می‌شود - دقیقاً همان جایگاهی که ANCM دارد.
/// بدون این، هیچ راهی نبود که رفتار IIS را روی ماشین توسعه (که ANCM ندارد) آزمود.
/// </summary>
public sealed class IisChildApplicationEmulator : IStartupFilter
{
    private readonly PathString _virtualPath;

    public IisChildApplicationEmulator(string virtualPath) => _virtualPath = new PathString(virtualPath);

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            // IIS فقط درخواست‌هایی را به این Application می‌دهد که زیر مسیرش باشند،
            // و همان پیشوند را از Path به PathBase منتقل می‌کند.
            if (context.Request.Path.StartsWithSegments(_virtualPath, out var remainder))
            {
                context.Request.PathBase = _virtualPath;
                context.Request.Path = remainder;
            }

            await nextMiddleware(context);
        });

        next(app);
    };
}
