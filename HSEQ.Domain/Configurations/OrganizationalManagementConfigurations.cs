using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class OrganizationalManagementConfigurations : IEntityTypeConfiguration<OrganizationalManagement>
    {
        public void Configure(EntityTypeBuilder<OrganizationalManagement> builder)
        {
            builder.ToTable("OrganizationalManagements");
            builder.HasKey(m => m.Key);

            builder.Property(m => m.Code)
                .IsRequired()
                .HasMaxLength(1);

            builder.Property(m => m.Title)
                .IsRequired();

            builder.HasIndex(m => m.Code).IsUnique();

            builder.HasMany(m => m.Activities)
                .WithOne(a => a.OrganizationalManagement)
                .HasForeignKey(a => a.OrganizationalManagementId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
