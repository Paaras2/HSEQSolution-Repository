using HSEQ.API.Model.Dtos;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Repositories;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // ارزش این سرویس در سه چیز خلاصه می‌شود: شمارش درست KPIها (فقط آخرین بازنگری هر
    // زنجیره، نه هر ردیف تاریخی)، تشخیص وضعیت بازبینی هر سند نسبت به امروز، و فشرده‌سازی
    // فهرست‌های بلند (فعالیت‌ها، نوع سند) به چیزی که واقعاً در یک نمودار قابل‌خواندن است.
    //
    // فرض مهم: CurrentReviewDate یعنی «موعد بازبینی بعدی» سند (رو به جلو)، نه تاریخ آخرین
    // بازبینیِ انجام‌شده - این از داده‌ی واقعی موجود (چند سند با CurrentReviewDate در آینده)
    // و از منطق ReviseAsync (تاریخ فعلیِ نسخه‌ی قبلی، تاریخ قبلیِ نسخه‌ی جدید می‌شود) استنباط
    // شده. اگر منظور شما از این فیلد چیز دیگری بود، KPIها و هشدارهای این داشبورد را باید
    // دوباره تعریف کرد.
    public class DashboardService : IDashboardService
    {
        private const int AlertLimit = 10;
        private const int TrendMonths = 12;
        private const int TopActivityCount = 10;
        private const int TopDocumentTypeCount = 7;

        private readonly IDocumentRepository _documentRepository;

        public DashboardService(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var today = DateTime.UtcNow.Date;
            var in7Days = today.AddDays(7);
            var in30Days = today.AddDays(30);

            var latest = _documentRepository.GetAllAsQueryable().WhereLatestRevision(_documentRepository);
            var active = latest.Where(d => d.IsActive);

            var totalDocuments = await latest.CountAsync();
            var activeDocuments = await active.CountAsync();

            var dueForReview = await active.CountAsync(d =>
                d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date >= today && d.CurrentReviewDate.Value.Date <= in30Days);
            var overdueReviews = await active.CountAsync(d =>
                d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date < today);

            var expired = await active
                .Where(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date < today)
                .OrderBy(d => d.CurrentReviewDate)
                .Take(AlertLimit)
                .ToListAsync();

            var within7 = await active
                .Where(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date >= today && d.CurrentReviewDate.Value.Date <= in7Days)
                .OrderBy(d => d.CurrentReviewDate)
                .Take(AlertLimit)
                .ToListAsync();

            // از روز ۸ تا ۳۰ - عمداً با فهرست بالا همپوشانی ندارد.
            var within30 = await active
                .Where(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date > in7Days && d.CurrentReviewDate.Value.Date <= in30Days)
                .OrderBy(d => d.CurrentReviewDate)
                .Take(AlertLimit)
                .ToListAsync();

            // قدیمی‌ترین‌ها اول - هرچه سند دیرتر ثبت شده و هنوز تاریخ نخورده، فوری‌تر است.
            var missingDate = await active
                .Where(d => d.CurrentReviewDate == null)
                .OrderBy(d => d.CreatedTime)
                .Take(AlertLimit)
                .ToListAsync();

            var byManagement = await active
                .GroupBy(d => new { d.OrganizationalManagement.Code, d.OrganizationalManagement.Title })
                .Select(g => new NamedCountDto { Code = g.Key.Code, Label = g.Key.Title, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var byActivityAll = await active
                .GroupBy(d => new { d.OrganizationalActivity.Code, d.OrganizationalActivity.Title })
                .Select(g => new NamedCountDto { Code = g.Key.Code, Label = g.Key.Title, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var noDateSet = await active.CountAsync(d => d.CurrentReviewDate == null);
            var onTrack = await active.CountAsync(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date > in30Days);

            var upcomingReviewDates = await active
                .Where(d => d.CurrentReviewDate != null && d.CurrentReviewDate.Value.Date >= today)
                .Select(d => d.CurrentReviewDate!.Value)
                .ToListAsync();

            var byTypeAll = await active
                .GroupBy(d => new { d.DocumentType.Code, d.DocumentType.Title })
                .Select(g => new NamedCountDto { Code = g.Key.Code, Label = g.Key.Title, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return new DashboardSummaryDto
            {
                TotalDocuments = totalDocuments,
                ActiveDocuments = activeDocuments,
                DueForReview = dueForReview,
                OverdueReviews = overdueReviews,

                ExpiredReviews = ToAlertDtos(expired, today),
                DueWithin7Days = ToAlertDtos(within7, today),
                DueWithin30Days = ToAlertDtos(within30, today),
                MissingReviewDate = ToAlertDtos(missingDate, today),

                DocumentsByManagement = byManagement,
                DocumentsByActivity = byActivityAll.Take(TopActivityCount).ToList(),
                ReviewStatus = new ReviewStatusBreakdownDto
                {
                    OnTrack = onTrack,
                    DueSoon = dueForReview,
                    Overdue = overdueReviews,
                    NoDateSet = noDateSet,
                },
                ReviewTrend = BuildMonthlyTrend(upcomingReviewDates, today),
                DocumentsByType = CollapseToTopNPlusOther(byTypeAll, TopDocumentTypeCount),
            };
        }

        // برای فهرست «بدون تاریخ بازبینی» هر دو فیلد تاریخ نال می‌مانند، پس اینجا شرطی است.
        private static List<DocumentAlertDto> ToAlertDtos(List<Document> documents, DateTime today)
        {
            return documents.Select(d => new DocumentAlertDto
            {
                Key = d.Key,
                Number = d.Number,
                Name = d.Name,
                ReviewDate = d.CurrentReviewDate,
                DaysUntilDue = d.CurrentReviewDate.HasValue
                    ? (d.CurrentReviewDate.Value.Date - today).Days
                    : null,
            }).ToList();
        }

        // ۱۲ ماه پیش‌رو از ماه جاری، شامل ماه‌های بدون هیچ سندی (صفر) - وگرنه نمودار خطی
        // با پرش بین ماه‌های دارای داده گمراه‌کننده می‌شد.
        private static List<MonthCountDto> BuildMonthlyTrend(List<DateTime> dates, DateTime today)
        {
            var counts = dates
                .GroupBy(d => new DateTime(d.Year, d.Month, 1))
                .ToDictionary(g => g.Key, g => g.Count());

            var start = new DateTime(today.Year, today.Month, 1);
            var result = new List<MonthCountDto>();
            for (var i = 0; i < TrendMonths; i++)
            {
                var monthStart = start.AddMonths(i);
                result.Add(new MonthCountDto
                {
                    MonthLabel = monthStart.ToString("yyyy-MM"),
                    Count = counts.TryGetValue(monthStart, out var count) ? count : 0,
                });
            }

            return result;
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
