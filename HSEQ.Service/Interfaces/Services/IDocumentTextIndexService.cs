namespace HSEQ.Service.Interfaces.Services
{
    // نمایه‌سازیِ دوباره‌ی متن فایل اسناد موجود.
    //
    // چرا لازم است: متن فایل فقط لحظه‌ی بارگذاری استخراج و ذخیره می‌شود. اسنادی که
    // پیش از این استخراج‌کننده وارد سامانه شده‌اند یا ستون متنشان خالی است یا متنی
    // دارند که با استخراج‌کننده‌ی قدیمی به دست آمده و برای جستجو بی‌فایده است. این
    // سرویس فایل‌های روی دیسک را دوباره می‌خواند و ستون را از نو می‌سازد.
    public interface IDocumentTextIndexService
    {
        // onlyMissing=true فقط اسنادی را می‌سازد که هنوز متنی ندارند - برای وقتی که
        // نمایه‌سازیِ کامل یک‌بار انجام شده و فقط چند سند تازه مانده است.
        Task<DocumentTextIndexResult> ReindexAsync(bool onlyMissing, CancellationToken cancellationToken = default);
    }

    public class DocumentTextIndexResult
    {
        // کل اسنادی که بررسی شدند.
        public int Total { get; set; }

        // متن استخراج شد و در دیتابیس نشست.
        public int Indexed { get; set; }

        // فایل باز شد ولی متنی نداشت - عملاً یعنی PDF اسکن‌شده یا تصویر.
        public int WithoutText { get; set; }

        // نام فایل در دیتابیس هست ولی فایلش روی دیسک نیست.
        public int FileMissing { get; set; }

        public int Failed { get; set; }

        public List<string> Problems { get; set; } = [];
    }
}
