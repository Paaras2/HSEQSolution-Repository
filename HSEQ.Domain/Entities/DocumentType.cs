using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Entities
{
    public class DocumentType : BaseEntity
    {
        public string Code { get; set; }
        public string Title { get; set; }
    }
}
