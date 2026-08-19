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

    // دسته‌بندی «پروژه» بدون انتخاب پروژه معنا ندارد؛ فقط برای دسته‌بندی «ستاد» پروژه اختیاری است.
    public class ProjectRequiredForProjectCategoryException : CustomException
    {
        public ProjectRequiredForProjectCategoryException()
            : base("برای دسته‌بندی «پروژه»، انتخاب پروژه الزامی است")
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

    // A revision chain is linear: each revision supersedes exactly one predecessor.
    // Thrown when a Document that has already been superseded is revised again,
    // which would fork the chain and produce two documents claiming the same
    // revision number.
    public class DocumentAlreadyRevisedException : CustomException
    {
        public DocumentAlreadyRevisedException()
            : base("این مدرک قبلاً بازنگری شده است. بازنگری بعدی باید روی آخرین نسخه انجام شود")
        {
        }
    }

    // A revision is a new content version of the document, so it always carries a
    // new file - there is no such thing as a revision that reuses the previous
    // revision's attachment.
    public class RevisionFileRequiredException : CustomException
    {
        public RevisionFileRequiredException() : base("برای ثبت بازنگری، بارگذاری فایل جدید الزامی است")
        {
        }
    }

    // Exactly one revision of a chain is current at a time. Thrown when an update
    // tries to re-activate a revision that a newer one has already superseded.
    public class SupersededDocumentCannotBeReactivatedException : CustomException
    {
        public SupersededDocumentCannotBeReactivatedException()
            : base("این نسخه بازنگری شده است و نمی‌توان دوباره فعالش کرد. برای نسخه جدید، آخرین بازنگری را بازنگری کنید")
        {
        }
    }

    // The Document row exists but its FileName does not resolve to a file on disk -
    // e.g. the upload folder was moved between environments. Distinct from
    // DocumentNotFoundException so the caller can tell "no such document" from
    // "document is there, its file is not".
    public class DocumentFileNotFoundException : CustomException
    {
        public DocumentFileNotFoundException() : base("فایل این مدرک روی سرور یافت نشد", 404)
        {
        }
    }
}