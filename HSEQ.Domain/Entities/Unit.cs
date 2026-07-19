using HSEQ.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Domain.Entities
{
    public class Unit : BaseEntity
    {
        public string Title { get; set; }


        //یعنی نوع این property یک مجموعه از Documentها است
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}