using Xunit;

// AppSettingFactory یک وضعیت ایستا (static) است که هنگام بالا آمدن هر میزبان از نو
// مقداردهی می‌شود. با اجرای موازی، دو میزبانِ هم‌زمان روی همان وضعیت می‌نوشتند و
// آزمون‌ها به‌صورت تصادفی شکست می‌خوردند - شکستی که علتش پیدا نمی‌شد.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
