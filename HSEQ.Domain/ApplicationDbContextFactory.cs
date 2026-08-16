using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HSEQ.Domain
{
    // Standard EF Core design-time factory. Used ONLY by the `dotnet ef` CLI tooling
    // (migrations add / database update / etc.) - never invoked by the running
    // application. Exists so EF tooling does not need to build the full ASP.NET Core
    // host (Program.cs) - including its startup migration/seed/app.Run() calls - just
    // to resolve DbContextOptions<ApplicationDbContext> at design time.
    //
    // The connection string here is a design-time-only fallback and is never used at
    // runtime; the real application continues to configure ApplicationDbContext from
    // appsettings.json via AddDomainLayerServices, unchanged. Override at the CLI with
    // `dotnet ef database update --connection "..."` to target a different server.
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=HSEQDb;Trusted_Connection=True;TrustServerCertificate=True");

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
