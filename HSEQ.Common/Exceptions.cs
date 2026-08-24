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

    // یک مدرک نمی‌تواند به خودش مرتبط شود - چنین ردیفی نه معنایی دارد و نه در فهرست
    // «مدارک مرتبط» قابل نمایش است.
    public class SelfDocumentRelationException : CustomException
    {
        public SelfDocumentRelationException()
            : base("یک مدرک را نمی‌توان به خودش مرتبط کرد")
        {
        }
    }

    // ارتباط میان دو مدرک، یک جفتِ بدون‌ترتیب است: اگر A به B مرتبط باشد، B هم به A
    // مرتبط است. پس ثبت دوباره‌ی همان جفت - در هر جهتی و با هر نوعی - رد می‌شود.
    // برای تغییر نوع ارتباط، ابتدا ارتباط موجود حذف و سپس دوباره ثبت می‌شود.
    public class DuplicateDocumentRelationException : CustomException
    {
        public DuplicateDocumentRelationException()
            : base("این دو مدرک از قبل به هم مرتبط شده‌اند")
        {
        }
    }

    public class DocumentRelationNotFoundException : CustomException
    {
        public DocumentRelationNotFoundException()
            : base("ارتباط مورد نظر یافت نشد", 404)
        {
        }
    }

    // ---- پنل ادمین: اطلاعات پایه و نقش کاربران ----

    // کدِ اطلاعات پایه داخل شماره‌ی مدارک حک می‌شود، پس در کل سامانه باید یکتا بماند.
    public class DuplicateMasterDataCodeException : CustomException
    {
        public DuplicateMasterDataCodeException(string code)
            : base($"کد «{code}» قبلاً ثبت شده است")
        {
        }
    }

    // طول کد هر نوع ثابت است (پروژه ۴، مدیریت ۱، فعالیت ۲، نوع سند ۲) چون ساختار
    // شماره‌ی مدرک بر همین طول‌ها بنا شده است.
    public class InvalidMasterDataCodeException : CustomException
    {
        public InvalidMasterDataCodeException(int expectedLength)
            : base($"کد باید دقیقاً {expectedLength} کاراکتر انگلیسی یا عدد باشد")
        {
        }
    }

    public class MasterDataItemNotFoundException : CustomException
    {
        public MasterDataItemNotFoundException() : base("ردیف اطلاعات پایه یافت نشد", 404)
        {
        }
    }

    // فعالیت سازمانی زیرمجموعه‌ی یک مدیریت است و بدون آن جایی در شماره‌ی مدرک ندارد.
    public class ManagementRequiredForActivityException : CustomException
    {
        public ManagementRequiredForActivityException()
            : base("برای فعالیت سازمانی، انتخاب مدیریت سازمانی الزامی است")
        {
        }
    }

    // اگر مدرکی به این ردیف وابسته باشد، غیرفعال‌کردنش فهرست‌های موجود را می‌شکند.
    public class MasterDataItemInUseException : CustomException
    {
        public MasterDataItemInUseException(int usageCount)
            : base($"این ردیف در {usageCount} مدرک استفاده شده و قابل غیرفعال‌سازی نیست")
        {
        }
    }

    // کسی نمی‌تواند نقش خودش را عوض کند - وگرنه یک مدیر سیستم می‌توانست ناخواسته
    // دسترسی خودش را قطع کند و راه بازگشتی نماند.
    public class CannotChangeOwnRoleException : CustomException
    {
        public CannotChangeOwnRoleException()
            : base("نقش خودتان را نمی‌توانید تغییر دهید")
        {
        }
    }

    // فقط مدیر سیستم می‌تواند مدیر سیستم بسازد یا حذف کند. مدیر اسناد فقط
    // می‌تواند مدیر اسناد تعریف کند - وگرنه راهی برای ارتقای خودش پیدا می‌کرد.
    public class InsufficientRoleToGrantException : CustomException
    {
        public InsufficientRoleToGrantException()
            : base("فقط مدیر سیستم می‌تواند نقش «مدیر سیستم» بدهد یا بگیرد")
        {
        }
    }

    // آخرین مدیر سیستم نباید حذف شود، وگرنه دیگر کسی نمی‌تواند نقش‌ها را مدیریت کند.
    public class LastAdminCannotBeRemovedException : CustomException
    {
        public LastAdminCannotBeRemovedException()
            : base("آخرین مدیر سیستم را نمی‌توان حذف یا تنزل داد")
        {
        }
    }

    public class MasterDataTitleRequiredException : CustomException
    {
        public MasterDataTitleRequiredException() : base("عنوان الزامی است")
        {
        }
    }

    // نقشی خارج از AppRole. مهم است که صریح رد شود: مقدار ۰ (پیش‌فرض enum) اگر
    // به دیتابیس می‌رسید، معنای مشخصی نداشت.
    public class InvalidUserRoleException : CustomException
    {
        public InvalidUserRoleException() : base("نقش انتخاب‌شده معتبر نیست")
        {
        }
    }

    public class InvalidUserPcodeException : CustomException
    {
        public InvalidUserPcodeException() : base("کد پرسنلی معتبر نیست")
        {
        }
    }
}
