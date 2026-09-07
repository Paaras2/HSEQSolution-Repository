using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // فایل‌های موجود روی دیسک را دوباره می‌خواند و ستون ExtractedText را از نو می‌سازد.
    //
    // ApplicationDbContext مستقیم استفاده می‌شود (نه مخزن) تا بشود دسته‌ای ذخیره کرد و
    // ستون متن را بدون بارگذاریِ کل موجودیت نوشت - همان الگوی سرویس ورود اسناد قدیمی.
    public class DocumentTextIndexService : IDocumentTextIndexService
    {
        // اندازه‌ی دسته: بین «تعداد رفت‌وبرگشت به دیتابیس» و «حجم تراکنش» تعادل است.
        private const int BatchSize = 50;

        // فهرست مشکلات فقط برای دیدن الگوی خطاست، نه گزارش کامل - طولانی شدنش
        // پاسخ را بی‌فایده می‌کند.
        private const int MaxReportedProblems = 50;

        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly IFileTextExtractionService _textExtraction;

        public DocumentTextIndexService(
            ApplicationDbContext context,
            IFileService fileService,
            IFileTextExtractionService textExtraction)
        {
            _context = context;
            _fileService = fileService;
            _textExtraction = textExtraction;
        }

        public async Task<DocumentTextIndexResult> ReindexAsync(bool onlyMissing, CancellationToken cancellationToken = default)
        {
            var result = new DocumentTextIndexResult();

            var query = _context.Set<Document>().AsNoTracking();
            if (onlyMissing)
                query = query.Where(d => d.ExtractedText == null);

            // فقط کلید و نام فایل خوانده می‌شود؛ خودِ ستون متن می‌تواند صدها کیلوبایت
            // باشد و برای بازسازی هیچ کاربردی ندارد.
            var targets = await query
                .OrderBy(d => d.Number)
                .Select(d => new { d.Key, d.Number, d.FileName })
                .ToListAsync(cancellationToken);

            result.Total = targets.Count;
            var pending = 0;

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var file = _fileService.OpenDocumentFile(target.FileName);
                    if (file is null)
                    {
                        result.FileMissing++;
                        AddProblem(result, $"{target.Number}: فایل روی دیسک پیدا نشد ({target.FileName})");
                        continue;
                    }

                    var text = _textExtraction.Extract(file, target.FileName);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        result.WithoutText++;
                        // متنِ قبلیِ بی‌اعتبار هم باید پاک شود، وگرنه نتیجه‌ی غلطِ
                        // استخراج‌کننده‌ی قدیمی برای همیشه در جستجو می‌ماند.
                        text = null;
                    }
                    else
                    {
                        result.Indexed++;
                    }

                    MarkExtractedText(target.Key, text);
                    pending++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    AddProblem(result, $"{target.Number}: {ex.Message}");
                }

                if (pending >= BatchSize)
                {
                    await SaveBatchAsync(cancellationToken);
                    pending = 0;
                }
            }

            if (pending > 0)
                await SaveBatchAsync(cancellationToken);

            return result;
        }

        // فقط همین یک ستون به‌روز می‌شود: موجودیت با کلیدش وصله می‌شود و بقیه‌ی
        // فیلدها دست‌نخورده می‌مانند، پس نه لازم است سند کامل خوانده شود و نه خطری
        // هست که مقدار پیش‌فرضِ یک فیلد روی داده‌ی واقعی نوشته شود.
        private void MarkExtractedText(Guid key, string? text)
        {
            var document = new Document { Key = key };
            _context.Attach(document);
            document.ExtractedText = text;
            _context.Entry(document).Property(d => d.ExtractedText).IsModified = true;
        }

        private async Task SaveBatchAsync(CancellationToken cancellationToken)
        {
            await _context.SaveChangesAsync(cancellationToken);
            // ردیاب تغییرات خالی می‌شود تا حافظه با هزاران موجودیتِ وصله‌شده پر نشود.
            _context.ChangeTracker.Clear();
        }

        private static void AddProblem(DocumentTextIndexResult result, string problem)
        {
            if (result.Problems.Count < MaxReportedProblems)
                result.Problems.Add(problem);
        }
    }
}
