using HSEQ.API.Domain;
using Microsoft.EntityFrameworkCore;
using HSEQ.API.Domain.Entities;

namespace HSEQ.API.Domain
{
    public class ApplicationDbContext : DbContext
    {
        
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

      
        public DbSet<Unit> Units { get; set; }
    }
}
