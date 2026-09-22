using HSEQ.API.Model.Dtos;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // ارزش این سرویس در سه چیز خلاصه می‌شود: شمارش درست KPIها (فقط آخرین بازنگری هر
    // زنجیره، نه هر ردیف تاریخی)، درجه‌بندیِ بدهیِ بازبینی به‌جای یک عددِ خام (سنِ
    // معوقات، سهمِ هر مدیریت)، و فشرده‌سازی فهرست‌های بلند (فعالیت‌ها، نوع سند) به
    // چیزی که واقعاً در یک نمودار قابل‌خواندن است.
    //
    // فرض مهم: CurrentReviewDate یعنی «موعد بازبینی بعدی» سند (رو به جلو)، نه تاریخ آخرین
    // بازبینیِ انجام‌شده - این از منطق ReviseAsync استنباط شده (تاریخ فعلیِ نسخه‌ی قبلی،
    // تاریخ قبلیِ نسخه‌ی جدید می‌شود). روی داده‌ی واقعی این پروژه، این فرض یک واقعیتِ
    // تلخ را آشکار می‌کند: اکثریتِ قریب‌به‌اتفاق اسناد فعال از موعدشان گذشته - داشبورد
    // باید همین را روشن بگوید، نه با میانگین‌گیری در چند نمودارِ خنثی پنهانش کند.
    public class DashboardService : IDashboardService
    {
        private const int TopActivityCount = 10;
        private const int TopDocumentTypeCount = 7;
        private const int TopOverdueManagementCount = 8;

        private readonly IDocumentRepository _documentRepository;

        public DashboardService(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var today = DateTime.UtcNow.Date;

            var latest = _documentRepository.GetAllAsQueryable().WhereLatestRevision(_documentRepository);
            var active = latest.Where(d => d.IsActive);

            var totalDocuments = await active.CountAsync();
            var overdueReviews = await active.CountAsync(d =>
                d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date < today);
            var missingReviewDateCount = await active.CountAsync(d => d.CurrentReviewDate == null);

            // به‌روز = دارای تاریخ و هنوز نرسیده به آن. سندِ بی‌تاریخ نه اینجا حساب
            // می‌شود نه در معوق - نمی‌شود گفت «به‌روز است» وقتی اصلاً معلوم نیست موعدش کِی است.
            var percentUpToDate = totalDocuments > 0
                ? Math.Round(100.0 * (totalDocuments - overdueReviews - missingReviewDateCount) / totalDocuments, 1)
                : 100.0;

            // سنِ معوقات: چند روز از موعد گذشته، نه صرفاً «معوق هست یا نیست». تعدادش کم
            // است (معمولاً چند صد سند حداکثر)، پس دسته‌بندی در حافظه از یک کوئریِ
            // CASE-پیچیده‌ی SQL هم ساده‌تر است هم مطمئن‌تر.
            var overdueDates = await active
                .Where(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date < today)
                .Select(d => d.CurrentReviewDate!.Value.Date)
                .ToListAsync();
            var overdueByAge = BuildOverdueAgeBuckets(overdueDates, today);

            // سهمِ معوق در هر مدیریت - هم تعدادِ کل هم تعدادِ معوق در یک رفت‌وبرگشت، تا
            // درصد از رویِ همان دو عدد ساخته شود، نه با یک کوئریِ دوم.
            var managementBreakdown = await active
                .GroupBy(d => new { d.OrganizationalManagement.Code, d.OrganizationalManagement.Title })
                .Select(g => new
                {
                    g.Key.Code,
                    g.Key.Title,
                    Total = g.Count(),
                    Overdue = g.Count(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date < today),
                })
                .ToListAsync();

            var overdueByManagement = managementBreakdown
                .Where(x => x.Overdue > 0)
                .Select(x => new NamedCountDto
                {
                    Code = x.Code,
                    Label = x.Title,
                    Count = (int)Math.Round(100.0 * x.Overdue / x.Total),
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Label)
                .Take(TopOverdueManagementCount)
                .ToList();

            var byActivityAll = await active
                .GroupBy(d => new { d.OrganizationalActivity.Code, d.OrganizationalActivity.Title })
                .Select(g => new NamedCountDto { Code = g.Key.Code, Label = g.Key.Title, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var byTypeAll = await active
                .GroupBy(d => new { d.DocumentType.Code, d.DocumentType.Title })
                .Select(g => new NamedCountDto { Code = g.Key.Code, Label = g.Key.Title, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return new DashboardSummaryDto
            {
                TotalDocuments = totalDocuments,
                PercentUpToDate = percentUpToDate,
                OverdueReviews = overdueReviews,
                MissingReviewDateCount = missingReviewDateCount,

                OverdueByAge = overdueByAge,
                OverdueByManagement = overdueByManagement,

                DocumentsByActivity = byActivityAll.Take(TopActivityCount).ToList(),
                DocumentsByType = CollapseToTopNPlusOther(byTypeAll, TopDocumentTypeCount),
            };
        }

        // مرزهای سن معوقات: هرچه سندی بیشتر از موعدش گذشته باشد، رفتار لازم برایش
        // فرق می‌کند - چند هفته تأخیر یک یادآوریِ ساده است، چند سال یک بدهیِ ساختاری
        // که باید جدا از کارِ روزمره مدیریت شود. ترتیب همیشه از کم‌فوریت به پرفوریت
        // برمی‌گردد (حتی بازه‌های صفر) چون خودِ توالی هم بخشی از پیام نمودار است.
        private static List<NamedCountDto> BuildOverdueAgeBuckets(List<DateTime> overdueDates, DateTime today)
        {
            var buckets = new (string Code, string Label, int MaxDays)[]
            {
                ("M1", "تا ۱ ماه", 30),
                ("M3", "۱ تا ۳ ماه", 90),
                ("Y1", "۳ ماه تا ۱ سال", 365),
                ("Y3", "۱ تا ۳ سال", 365 * 3),
                ("MAX", "بیش از ۳ سال", int.MaxValue),
            };

            var counts = new int[buckets.Length];
            foreach (var date in overdueDates)
            {
                var daysOverdue = (today - date).Days;
                for (var i = 0; i < buckets.Length; i++)
                {
                    if (daysOverdue <= buckets[i].MaxDays)
                    {
                        counts[i]++;
                        break;
                    }
                }
            }

            return buckets.Select((b, i) => new NamedCountDto { Code = b.Code, Label = b.Label, Count = counts[i] }).ToList();
        }

        // فهرست‌های بلند (فعالیت، نوع سند) را به N مورد پرتکرار + یک ردیف «سایر» فشرده می‌کند
        // تا نمودار عملاً قابل‌خواندن بماند.
        private static List<NamedCountDto> CollapseToTopNPlusOther(List<NamedCountDto> all, int topN)
        {
            if (all.Count <= topN)
                return all;

            var top = all.Take(topN).ToList();
            var otherCount = all.Skip(topN).Sum(x => x.Count);
            if (otherCount > 0)
                top.Add(new NamedCountDto { Code = "OTHER", Label = "سایر", Count = otherCount });

            return top;
        }
    }
}
