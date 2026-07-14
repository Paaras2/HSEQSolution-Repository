using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Common
{
    public class DocumentNotFoundException : CustomException
    {
        public DocumentNotFoundException() : base("مدرک یافت نشد")
        {

        }
    }

    public class Exceptions : CustomException
    {
        public Exceptions() : base("مدرک یافت نشد")
        {

        }
    }

}
