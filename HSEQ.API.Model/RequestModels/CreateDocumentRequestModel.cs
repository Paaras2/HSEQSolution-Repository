using Microsoft.AspNetCore.Http;
using System;

namespace HSEQ.API.Model.RequestModels
{
    public class CreateDocumentRequestModel
    {
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }
        public Guid? RelatedDocumentId { get; set; }

        //Namyande yek file upload shode dar asp.net
        public IFormFile File { get; set; }

        // Master-data references the server uses to generate Document.Number.
        // Number itself is never accepted from the client - see IDocumentNumberGeneratorService.
        public Guid ProjectId { get; set; }
        public Guid OrganizationalManagementId { get; set; }
        public Guid OrganizationalActivityId { get; set; }
        public Guid DocumentTypeId { get; set; }
    }
}