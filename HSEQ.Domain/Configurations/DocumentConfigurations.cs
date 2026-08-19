using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Configurations
{
    public class DocumentConfigurations : IEntityTypeConfiguration<Document>
    {
        public void Configure(EntityTypeBuilder<Document> builder)
        {
            builder.ToTable("Documents").HasKey(r => r.Key);

            // Max length: PPPP(4)+O(1)+AA(2)+DD(2)+SSS(3)+R(up to 3 for "Z99") = 15.
            builder.Property(d => d.Number)
                .IsRequired()
                .HasMaxLength(15);

            builder.HasIndex(d => d.Number).IsUnique();

            // بدون محدودیت طول، چون متن کامل فایل (تا سقف تعیین‌شده در سرویس استخراج) اینجا نگه داشته می‌شود.
            builder.Property(d => d.ExtractedText).HasColumnType("nvarchar(max)");

            // فقط اسناد دسته‌ی «پروژه» پروژه دارند؛ اسناد «ستاد» این FK را خالی می‌گذارند.
            builder.HasOne(d => d.Project)
                .WithMany(p => p.Documents)
                .HasForeignKey(d => d.ProjectId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(d => d.OrganizationalManagement)
                .WithMany()
                .HasForeignKey(d => d.OrganizationalManagementId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(d => d.OrganizationalActivity)
                .WithMany()
                .HasForeignKey(d => d.OrganizationalActivityId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(d => d.DocumentType)
                .WithMany()
                .HasForeignKey(d => d.DocumentTypeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
