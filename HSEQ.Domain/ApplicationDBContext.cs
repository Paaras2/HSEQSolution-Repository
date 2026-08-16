using HSEQ.Domain;
using HSEQ.Domain.Common;
using HSEQ.Domain.DocumentNumbering;
using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace HSEQ.Domain
{
    public class ApplicationDbContext : DbContext
    {
        
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

       

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);
            //اشاره به تمامی موجودیت ها 
            var entitiesAssembly = typeof(IEntity).Assembly;
            modelBuilder.RegisterAllEntities<IEntity>(entitiesAssembly);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Global Document Serial Number (SSS). Starts at 1, increments by 1, never
            // derives its value from Documents (not MAX+1) - see Step 3 of the Document
            // Numbering implementation. NEXT VALUE FOR allocation is not transactional,
            // so a value is never returned to the pool even if the surrounding insert
            // is rolled back or the document is later deleted/deactivated.
            modelBuilder.HasSequence<int>(DocumentSerialSequenceInfo.Name, DocumentSerialSequenceInfo.Schema)
                .StartsAt(1)
                .IncrementsBy(1)
                .HasMin(1)
                .HasMax(999);
        }
        
    }


    public static class ModelBuilderExtensions
    {
        public static void RegisterAllEntities<BaseType>(this ModelBuilder modelBuilder, params Assembly[] assemblies)
        {
            IEnumerable<Type> types = assemblies.SelectMany(a => a.GetExportedTypes())
                .Where(c => c.IsClass && !c.IsAbstract && c.IsPublic && typeof(BaseType).IsAssignableFrom(c));

            foreach (Type type in types)
                modelBuilder.Entity(type);
        }
    }
}