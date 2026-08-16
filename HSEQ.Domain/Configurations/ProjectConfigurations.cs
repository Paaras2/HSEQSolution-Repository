using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSEQ.Domain.Configurations
{
    public class ProjectConfigurations : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Projects");
            builder.HasKey(p => p.Key);

            builder.Property(p => p.Code)
                .IsRequired()
                .HasMaxLength(4);

            builder.Property(p => p.Title)
                .IsRequired();

            builder.HasIndex(p => p.Code).IsUnique();

            builder.HasMany(p => p.Documents)
                .WithOne(d => d.Project)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
