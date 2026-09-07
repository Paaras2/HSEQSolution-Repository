namespace HSEQ.Service.Interfaces.Services
{
    // ورود یک‌باره‌ی اسناد سامانه‌ی قدیمی. عمداً سرویس جداست و به هیچ کنترلری وصل نیست:
    // این کار از خط فرمان و به‌صورت آگاهانه اجرا می‌شود، نه از رابط کاربری.
    //
    // مانیفست ورودی را یک اسکریپت جدا از فایل اکسل می‌سازد (دات‌نت بدون پکیج جدید xlsx
    // نمی‌خواند)، ولی درج واقعی از همین‌جا و از راه EF انجام می‌شود تا از همان موجودیت‌ها
    // و قیدهای دیتابیس عبور کند.
    public interface ILegacyDocumentImportService
    {
        Task<LegacyImportResult> ImportAsync(string manifestPath, string sourceDirectory, bool dryRun);
    }

    public class LegacyImportResult
    {
        public int Total { get; set; }
        public int Inserted { get; set; }
        public int AlreadyExisted { get; set; }
        public int Failed { get; set; }
        public List<string> Problems { get; set; } = [];
    }
}
