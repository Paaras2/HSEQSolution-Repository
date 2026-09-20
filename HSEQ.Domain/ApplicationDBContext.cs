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
        
        // currentUser اختیاری است تا factory زمانِ طراحی (مهاجرت‌ها) و آزمون‌هایی که
        // context را فقط با options می‌سازند نشکنند؛ در زمان اجرا از DI تزریق می‌شود.
        private readonly ICurrentUserAccessor _currentUser;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserAccessor currentUser = null)
            : base(options)
        {
            _currentUser = currentUser;
        }

        // ردِ حسابرسی به‌صورت متمرکز اینجا زده می‌شود، نه در سرویس‌ها: هر مسیری که سند
        // یا داده‌ی پایه را تغییر دهد از همین‌جا رد می‌شود، پس نه جا می‌افتد و نه از
        // بدنه‌ی درخواست قابل جعل است. ModifiedByPCode همیشه از هویتِ تأییدشده گرفته
        // می‌شود؛ CreatedByPCode اگر سرویس صریحاً گذاشته باشد حفظ می‌شود.
        public override int SaveChanges()
        {
            StampAudit();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampAudit();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void StampAudit()
        {
            var pcode = _currentUser?.PCode;
            var now = DateTime.Now;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.CreatedTime == default) entry.Entity.CreatedTime = now;
                    entry.Entity.CreatedByPCode ??= pcode;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.ModifiedDate = now;
                    entry.Entity.ModifiedByPCode = pcode;
                }
            }
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