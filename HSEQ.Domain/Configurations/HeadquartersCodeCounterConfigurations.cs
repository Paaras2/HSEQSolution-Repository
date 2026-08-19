using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class HeadquartersCodeCounterConfigurations : IEntityTypeConfiguration<HeadquartersCodeCounter>
    {
        public void Configure(EntityTypeBuilder<HeadquartersCodeCounter> builder)
        {
            builder.ToTable("HeadquartersCodeCounters");
            builder.HasKey(c => c.Key);

            builder.Property(c => c.Code5)
                .IsRequired()
                .HasMaxLength(5);

            // یکتا بودن همین ایندکس، تضمین دیتابیسی نبود رقابت دو تخصیص هم‌زمان روی یک کد است.
            builder.HasIndex(c => c.Code5).IsUnique();
        }
    }
}
