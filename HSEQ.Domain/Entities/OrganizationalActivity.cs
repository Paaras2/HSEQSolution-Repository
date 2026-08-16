using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Entities
{
    public class OrganizationalActivity : BaseEntity
    {
        public string Code { get; set; }
        public string Title { get; set; }

        public Guid OrganizationalManagementId { get; set; }
        public virtual OrganizationalManagement OrganizationalManagement { get; set; }
    }
}
