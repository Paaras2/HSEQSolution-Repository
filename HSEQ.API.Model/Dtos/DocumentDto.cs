using HSEQ.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.API.Model.Dtos
{
    public class DocumentDto : BaseDto
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public string? FormerReviewShamsiDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }
        public string? CurrentReviewShamsiDate { get; set; }
        public DocumentVersion LastVersion { get; set; }
        public Guid? RelatedDocumentId { get; set; }
        public string? RelatedDocumentNumber { get; set; }
        public string FileName { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitTitle { get; set; }
        public string? File { set; get; }
        public int CreatedByPCode { get; set; }
    }
}
