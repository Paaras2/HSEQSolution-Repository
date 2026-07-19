using HSEQ.Common;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.API.Model.RequestModels
{
    public class CreateDocumentRequestModel
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public DateTime? FormerReviewDate { get; set; }
        public DateTime? CurrentReviewDate { get; set; }
        public DocumentVersion LastVersion { get; set; }
        public Guid? RelatedDocumentId { get; set; }

        //Namyande yek file upload shode dar asp.net
        public IFormFile File { get; set; }
        public Guid UnitId { get; set; }
    }
}
