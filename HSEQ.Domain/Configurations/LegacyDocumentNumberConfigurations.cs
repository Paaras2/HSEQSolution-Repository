using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class LegacyDocumentNumberConfigurations : IEntityTypeConfiguration<LegacyDocumentNumber>
    {
        public void Configure(EntityTypeBuilder<LegacyDocumentNumber> builder)
        {
            builder.ToTable("LegacyDocumentNumbers");
            builder.HasKey(l => l.Key);

            builder.Property(l => l.RawNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(l => l.Code5).HasMaxLength(5);
            builder.Property(l => l.RevisionSuffix).HasMaxLength(10);
            builder.Property(l => l.ManagementCode).HasMaxLength(1);
            builder.Property(l => l.ActivityCode).HasMaxLength(2);
            builder.Property(l => l.DocumentTypeCode).HasMaxLength(2);
            builder.Property(l => l.Name).HasMaxLength(500);
            builder.Property(l => l.UnitLabel).HasMaxLength(200);
            builder.Property(l => l.LastEditShamsiDate).HasMaxLength(20);
            builder.Property(l => l.CurrentEditShamsiDate).HasMaxLength(20);
            builder.Property(l => l.CurrentVersion).HasMaxLength(5);

            // چند ردیف اکسل کد۵حرفی ندارند (رشته‌های ناقص/استثنایی)، پس ایندکس یکتا نیست -
            // فقط برای سریع‌کردن محاسبه‌ی «آخرین سریال هر کد» در Seed است.
            builder.HasIndex(l => l.Code5);
        }
    }
}
