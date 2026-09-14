using HSEQ.API.ServiceConfiguration;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// ترجمه‌ی خطای دیتابیس HSEQ برای اپراتور سرور. خروجی‌اش روی کنسول و در لاگ می‌آید،
/// پس رمزِ رشته‌ی اتصال هرگز نباید در آن باشد.
/// </summary>
public class HseqDatabaseDiagnosticsTests
{
    private const string ConnectionWithSecret =
        "Server=db-host;Database=HSEQDb;User Id=hseq-login;Password=NeverPrintMe-123;TrustServerCertificate=True";

    [Fact]
    public void The_target_names_server_database_and_login_but_never_the_password()
    {
        var target = HseqDatabaseDiagnostics.Target(ConnectionWithSecret);

        Assert.Contains("db-host", target);
        Assert.Contains("HSEQDb", target);
        Assert.Contains("hseq-login", target);
        Assert.DoesNotContain("NeverPrintMe", target);
    }

    [Fact]
    public void A_non_sql_failure_is_described_by_its_innermost_cause_without_the_password()
    {
        var failure = new InvalidOperationException("outer wrapper", new TimeoutException("the real cause"));

        var text = HseqDatabaseDiagnostics.Describe(failure, ConnectionWithSecret);

        Assert.Contains("the real cause", text);
        Assert.DoesNotContain("NeverPrintMe", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("this is ;;; not = a connection string")]
    public void A_missing_or_broken_connection_string_does_not_throw(string? connectionString)
    {
        Assert.False(string.IsNullOrWhiteSpace(HseqDatabaseDiagnostics.Target(connectionString!)));
        Assert.False(string.IsNullOrWhiteSpace(HseqDatabaseDiagnostics.Describe(new Exception("x"), connectionString)));
    }
}
