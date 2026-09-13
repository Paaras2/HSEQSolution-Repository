using System.Collections.Concurrent;
using HSEQ.Shared.Services.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// وارسیِ هنگام راه‌اندازی یک قرارداد دارد که نباید بشکند: هر چه پیش بیاید،
/// استثنا به بیرون نمی‌دهد.
///
/// اگر بدهد، برنامه اصلاً بالا نمی‌آید و IIS خطای ۵۰۰.۳۰ می‌دهد - یعنی همان کدی که
/// قرار بود عیب‌یابی را آسان کند، خودش سامانه را از کار می‌اندازد، و آن هم فقط روی
/// سروری که دیتابیس UM را ندارد. دقیقاً همان‌جا که این وارسی بیشترین ارزش را دارد.
/// </summary>
public class UmDatabaseProbeTests
{
    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentBag<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Noop();
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));

        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    [Fact]
    public async Task A_connection_string_that_cannot_be_parsed_is_reported_not_thrown()
    {
        var logger = new CapturingLogger();

        await UmDatabaseProbe.RunAsync("this is not a connection string at all;;;=", logger);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task A_server_that_does_not_answer_is_reported_not_thrown()
    {
        var logger = new CapturingLogger();

        await UmDatabaseProbe.RunAsync(
            "Server=no-such-host.invalid;Database=UserManagement;User Id=x;Password=y;" +
            "TrustServerCertificate=True;Connect Timeout=1",
            logger);

        var critical = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Critical));
        Assert.Contains("no-such-host.invalid", critical.Message);
    }

    /// <summary>
    /// پیام خرابی باید نام سرور و دیتابیس را بگوید، ولی رمز داخل رشته‌ی اتصال هرگز
    /// نباید به لاگ برسد - لاگ stdout روی سرور خوانده و گاهی جابه‌جا می‌شود.
    /// </summary>
    [Fact]
    public async Task The_password_never_reaches_the_log()
    {
        var logger = new CapturingLogger();

        await UmDatabaseProbe.RunAsync(
            "Server=no-such-host.invalid;Database=UserManagement;User Id=x;Password=SuperSecret123;" +
            "TrustServerCertificate=True;Connect Timeout=1",
            logger);

        Assert.All(logger.Entries, e => Assert.DoesNotContain("SuperSecret123", e.Message));
    }
}
