namespace HSEQ.API.Model.Dtos
{
    // یک ستونِ قابل‌انتخاب برای خروجی اکسلِ اسناد - خروجیِ GET /Search/export/columns.
    public class ExportColumnDto
    {
        public string Key { get; set; }
        public string Label { get; set; }
        // آیا این ستون در مجموعه‌ی پیش‌فرض (بدون سفارشی‌سازی) هست - رابطِ کاربری
        // چک‌باکس‌ها را با همین مقدار اولیه می‌سازد.
        public bool IsDefault { get; set; }
    }
}
