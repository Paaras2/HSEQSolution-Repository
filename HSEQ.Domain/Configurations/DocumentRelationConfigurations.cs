using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class DocumentRelationConfigurations : IEntityTypeConfiguration<DocumentRelation>
    {
        public void Configure(EntityTypeBuilder<DocumentRelation> builder)
        {
            builder.ToTable("DocumentRelations").HasKey(r => r.Key);

            builder.Property(r => r.Note).HasMaxLength(500);

            // یک جفت مدرک بیش از یک بار در همین جهت ثبت نمی‌شود. جهت معکوس را ایندکس
            // نمی‌تواند پوشش دهد (کلید مرکب مرتب است)، پس DocumentRelationService پیش از
            // درج، هر دو جهت را بررسی می‌کند - این ایندکس لایه‌ی دوم دفاع است.
            builder.HasIndex(r => new { r.SourceDocumentId, r.TargetDocumentId }).IsUnique();

            // برای فهرست‌کردن ارتباط‌های یک مدرک از سمت مقصد. سمت مبدأ از ایندکس یکتای
            // بالا استفاده می‌کند و ایندکس جداگانه لازم ندارد.
            builder.HasIndex(r => r.TargetDocumentId);

            // Restrict نه Cascade: SQL Server دو مسیر Cascade به یک جدول را نمی‌پذیرد،
            // و مدارک هم اصلاً حذف فیزیکی نمی‌شوند (DeleteAsync فقط IsActive را false
            // می‌کند)، پس این محدودیت در عمل هرگز فعال نمی‌شود.
            builder.HasOne(r => r.SourceDocument)
                .WithMany()
                .HasForeignKey(r => r.SourceDocumentId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.TargetDocument)
                .WithMany()
                .HasForeignKey(r => r.TargetDocumentId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
