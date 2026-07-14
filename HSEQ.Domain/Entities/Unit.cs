using HSEQ.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace HSEQ.API.Domain.Entities
{
    public class Unit:BaseEntity
    {
        [Key]
        public Guid Key { get; set; }
        public string Title { get; set; }

    }
}
