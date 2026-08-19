using HSEQ.Common;
using System;

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
        public int? ContentRevision { get; set; }
        public int SerialNumber { get; set; }
        // The revision this document supersedes, i.e. the previous link in the
        // revision chain. Null for a document's first revision.
        public Guid? RelatedDocumentId { get; set; }
        public string? RelatedDocumentNumber { get; set; }

        // True when a newer revision of this document exists. Distinguishes a document
        // that went inactive because it was revised from one that was deactivated
        // outright - both have IsActive == false.
        public bool IsSuperseded { get; set; }

        public string FileName { get; set; }
        public DocumentCategory Category { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid OrganizationalManagementId { get; set; }
        public Guid OrganizationalActivityId { get; set; }
        public Guid DocumentTypeId { get; set; }
        public string? File { set; get; }
        public int CreatedByPCode { get; set; }
    }
}