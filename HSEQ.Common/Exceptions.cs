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



    public class ProjectNotFoundException : CustomException
    {
        public ProjectNotFoundException() : base("پروژه یافت نشد")
        {
        }
    }

    public class OrganizationalManagementNotFoundException : CustomException
    {
        public OrganizationalManagementNotFoundException() : base("مدیریت سازمانی یافت نشد")
        {
        }
    }

    public class OrganizationalActivityNotFoundException : CustomException
    {
        public OrganizationalActivityNotFoundException() : base("فعالیت سازمانی یافت نشد")
        {
        }
    }

    public class DocumentTypeNotFoundException : CustomException
    {
        public DocumentTypeNotFoundException() : base("نوع مدرک یافت نشد")
        {
        }
    }

    // Thrown when a component (or combination of components) of a Document Number
    // fails structural validation - invalid length, invalid characters, invalid
    // serial/revision range, etc. See HSEQ.Domain.DocumentNumbering.
    public class InvalidDocumentCodeException : CustomException
    {
        public InvalidDocumentCodeException(string message) : base(message)
        {
        }
    }

    // Defensive, should not normally be reachable: the global SEQUENCE-backed serial
    // number guarantees uniqueness by construction. Thrown only if a generated Document
    // Number is somehow already present in the database.
    public class DuplicateDocumentNumberException : CustomException
    {
        public DuplicateDocumentNumberException() : base("شماره مدرک تولید شده تکراری است")
        {
        }
    }
}