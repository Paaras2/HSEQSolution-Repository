using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// محافظِ معماری: پیشوند ‎/api‎ فقط یک مالک دارد - میزبان.
///
/// این آزمون‌ها به یک درخواست HTTP وابسته نیستند؛ خودِ اعلان‌های مسیر را می‌خوانند.
/// اگر کسی روزی «api/» را دوباره به یک ‎[Route]‎ اضافه کند، اینجا می‌شکند - پیش از
/// آنکه روی سرور به‌صورت ۴۰۴ در مسیر ورود ظاهر شود.
/// </summary>
public class RouteOwnershipTests
{
    [Fact]
    public void No_controller_declares_the_api_prefix_itself()
    {
        var offenders = ControllerTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>()
                                    .Select(route => new { type.Name, route.Template }))
            .Where(x => x.Template is not null &&
                        x.Template.TrimStart('/').StartsWith("api", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Name} → [Route(\"{x.Template}\")]")
            .ToList();

        Assert.True(offenders.Count == 0,
            "پیشوند api متعلق به میزبان است (Application با مسیر /api در IIS، و UsePathBase " +
            "روی Kestrel). این کنترلرها دوباره اعلامش کرده‌اند و مسیر عمومی را دوتایی " +
            "می‌کنند:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Every_controller_declares_a_route_template()
    {
        var offenders = ControllerTypes()
            .Where(type => !type.GetCustomAttributes<RouteAttribute>().Any())
            .Select(type => type.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "کنترلر بدون [Route] به قرارداد عمومی گره نمی‌خورد: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// همان فهرست را از خودِ موتور مسیریابی می‌خواند، نه از روی attributeها - پس
    /// قراردادهای سراسری و هر چیزی که در زمان اجرا مسیر می‌سازد هم پوشش داده می‌شود.
    /// </summary>
    [Fact]
    public void No_resolved_endpoint_pattern_starts_with_api()
    {
        using var factory = new HseqApiFactory(HostingModel.KestrelAtRoot);
        _ = factory.CreateClient();

        var descriptions = factory.Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Select(item => item.RelativePath)
            .Where(path => path is not null)
            .ToList();

        Assert.NotEmpty(descriptions);

        var offenders = descriptions
            .Where(path => path!.TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offenders.Count == 0,
            "این مسیرها پیشوند api را داخل خودشان دارند: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// مسیر ورود دقیقاً همان چیزی است که کلاینت صدا می‌زند: ‎Auth/login‎ زیر پیشوند.
    /// </summary>
    [Fact]
    public void Login_endpoint_is_registered_exactly_where_the_client_calls_it()
    {
        using var factory = new HseqApiFactory(HostingModel.KestrelAtRoot);
        _ = factory.CreateClient();

        var paths = factory.Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Select(item => $"{item.HttpMethod} /{item.RelativePath}")
            .ToList();

        Assert.Contains("POST /Auth/login", paths);
    }

    private static IEnumerable<Type> ControllerTypes() =>
        typeof(Program).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);
}
