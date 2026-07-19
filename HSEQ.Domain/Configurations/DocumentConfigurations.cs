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

            builder.HasOne(d => d.Unit).WithMany(static u => u.Documents).HasForeignKey(d => d.UnitId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
