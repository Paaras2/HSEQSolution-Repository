using HSEQ.API.Domain.Entities;
using HSEQ.Common;
using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Entities
{
    public class Document : BaseEntity
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }
        public DocumentVersion LastVersion { get; set; }
        public Guid? RelatedDocumentId { get; set; }
        public string FileName { get; set; }
        public Guid UnitId { get; set; }
        public virtual Unit Unit { get; set; }
        public int CreatedByPCode { get; set; }
    }
}
