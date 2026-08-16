using Microsoft.AspNetCore.Http;
using System;

namespace HSEQ.API.Model.RequestModels
{
    public class UpdateDocumentRequestModel : BaseUpdateRequestModel
    {
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }
        public Guid? RelatedDocumentId { get; set; }
        public IFormFile? File { get; set; }

        // Number, Revision, Project, Organizational Management/Activity classification
        // etc. are the immutable, server-generated identity of the Document and are
        // intentionally NOT present here - they cannot be changed through a normal
        // metadata update.
    }
}