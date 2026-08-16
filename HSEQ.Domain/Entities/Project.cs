using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Entities
{
    public class Project : BaseEntity
    {
        public string Code { get; set; }
        public string Title { get; set; }

        // Determines whether Documents under this Project use the compound
        // major+content revision scheme (A01, A02, ...) instead of plain A-Z.
        public bool IsProjectRelated { get; set; }

        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
