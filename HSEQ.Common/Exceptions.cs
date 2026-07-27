using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Common
{
    public class ExternalAuthException : Exception
    {
        public int Code { get; }

        public ExternalAuthException(string message, int code)
            : base(message)
        {
            Code = code;
        }
    }

    public class DocumentNotFoundException : CustomException
    {
        public DocumentNotFoundException() : base("مدرک یافت نشد")
        {

        }
    }



    public class UnitNotFoundException : CustomException
    {
        public UnitNotFoundException() : base("واحد یافت نشد")
        {
        }
    }
}



