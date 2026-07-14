using HSEQ.API.Domain.Entities;
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
    public class UnitConfigurations : IEntityTypeConfiguration<Unit>
    {
        public void Configure(EntityTypeBuilder<Unit> builder)
        {
            builder.ToTable("Units");
            builder.HasKey(a => a.Key);

            builder.HasMany(u => u.Documents).WithOne(d => d.Unit).HasForeignKey(d => d.UnitId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
