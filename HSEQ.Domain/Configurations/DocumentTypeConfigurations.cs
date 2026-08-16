using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class DocumentTypeConfigurations : IEntityTypeConfiguration<DocumentType>
    {
        public void Configure(EntityTypeBuilder<DocumentType> builder)
        {
            builder.ToTable("DocumentTypes");
            builder.HasKey(t => t.Key);

            builder.Property(t => t.Code)
                .IsRequired()
                .HasMaxLength(2);

            builder.Property(t => t.Title)
                .IsRequired();

            builder.HasIndex(t => t.Code).IsUnique();
        }
    }
}
