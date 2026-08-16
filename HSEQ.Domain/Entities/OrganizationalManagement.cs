using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Entities
{
    public class OrganizationalManagement : BaseEntity
    {
        public string Code { get; set; }
        public string Title { get; set; }

        public virtual ICollection<OrganizationalActivity> Activities { get; set; } = new List<OrganizationalActivity>();
    }
}
