using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class OrganizationalActivityConfigurations : IEntityTypeConfiguration<OrganizationalActivity>
    {
        public void Configure(EntityTypeBuilder<OrganizationalActivity> builder)
        {
            builder.ToTable("OrganizationalActivities");
            builder.HasKey(a => a.Key);

            builder.Property(a => a.Code)
                .IsRequired()
                .HasMaxLength(2);

            builder.Property(a => a.Title)
                .IsRequired();

            // Activity codes are only meaningful within their parent Management
            // (e.g. "GE" repeats under every Management) - not globally unique.
            builder.HasIndex(a => new { a.OrganizationalManagementId, a.Code }).IsUnique();
        }
    }
}
