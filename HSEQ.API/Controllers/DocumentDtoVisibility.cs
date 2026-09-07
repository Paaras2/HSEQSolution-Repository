using System.Security.Claims;
using HSEQ.API.Model.Dtos;

namespace HSEQ.API.Controllers
{
    // پرچمِ «خارج از کدینگ» فقط برای کسی که می‌تواند سند مدیریت کند (Admin یا
    // DocumentManager) در پاسخ می‌ماند؛ برای کاربرِ فقط-مشاهده همین‌جا پاک می‌شود - نه
    // فقط پنهانش کند رابط کاربری. همان مرزی که کلاینت با hasCapability('documents:manage')
    // اعمال می‌کند، اینجا هم تکرار شده تا کسی که مستقیم به API بزند هم آن را نبیند.
    internal static class DocumentDtoVisibility
    {
        private static bool CanSeeOutsideCoding(ClaimsPrincipal user) =>
            user.IsInRole("Admin") || user.IsInRole("DocumentManager");

        internal static void ApplyOutsideCodingVisibility(DocumentDto? item, ClaimsPrincipal user)
        {
            if (item is null || CanSeeOutsideCoding(user)) return;
            item.IsOutsideCodingStructure = false;
        }

        internal static void ApplyOutsideCodingVisibility(IEnumerable<DocumentDto> items, ClaimsPrincipal user)
        {
            if (CanSeeOutsideCoding(user)) return;
            foreach (var item in items)
                item.IsOutsideCodingStructure = false;
        }
    }
}
