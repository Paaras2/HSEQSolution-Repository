using System.Text.Json;
using System.Text.Json.Serialization;
using HSEQ.Common;
using HSEQ.Domain;
using HSEQ.Domain.Entities;
using HSEQ.Service.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HSEQ.Service.Services.Services
{
    // ورود اسناد سامانه‌ی قدیمی از روی مانیفست JSON.
    //
    // سه قاعده‌ی این سرویس:
    //  ۱) هرگز چیزی را حذف یا بازنویسی نمی‌کند - سندی که شماره‌اش از قبل هست رد می‌شود.
    //  ۲) شماره‌ها از مانیفست خوانده می‌شوند نه از مولد شماره: این‌ها شماره‌های تاریخیِ
    //     صادرشده‌اند و باید عیناً حفظ شوند. شمارنده‌های HeadquartersCodeCounter از قبل
    //     با همین آرشیو مقداردهی شده‌اند، پس سندِ جدیدِ بعدی با این‌ها تداخل نمی‌کند.
    //  ۳) اگر کپی فایل شکست بخورد، رکورد آن سند هم درج نمی‌شود - سندِ بدون فایل بدتر از
    //     سندِ نبوده است.
    public class LegacyDocumentImportService : ILegacyDocumentImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileTextExtractionService _textExtraction;

        // اسناد واردشده صاحب انسانی ندارند؛ صفر یعنی «از سامانه‌ی قدیمی آمده».
        private const int ImportPCode = 0;

        public LegacyDocumentImportService(
            ApplicationDbContext context,
            IFileTextExtractionService textExtraction)
        {
            _context = context;
            _textExtraction = textExtraction;
        }

        private sealed class ManifestRoot
        {
            [JsonPropertyName("records")]
            public List<ManifestRecord> Records { get; set; } = [];
        }

        private sealed class ManifestRecord
        {
            [JsonPropertyName("number")] public string Number { get; set; } = "";
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("serialNumber")] public int SerialNumber { get; set; }
            [JsonPropertyName("lastVersion")] public int LastVersion { get; set; }
            [JsonPropertyName("isEnglishVersion")] public bool IsEnglishVersion { get; set; }

            // مدارکی که شماره‌شان با ساختار کدِ فعلی نمی‌خواند با این پرچم وارد می‌شوند.
            // پیش‌فرضش false است، پس مانیفست‌های قبلی بدون تغییر کار می‌کنند.
            [JsonPropertyName("isOutsideCodingStructure")] public bool IsOutsideCodingStructure { get; set; }
            [JsonPropertyName("managementCode")] public string ManagementCode { get; set; } = "";
            [JsonPropertyName("activityCode")] public string ActivityCode { get; set; } = "";
            [JsonPropertyName("documentTypeCode")] public string DocumentTypeCode { get; set; } = "";
            [JsonPropertyName("formerReviewDate")] public string? FormerReviewDate { get; set; }
            [JsonPropertyName("currentReviewDate")] public string? CurrentReviewDate { get; set; }
            [JsonPropertyName("sourceFile")] public string SourceFile { get; set; } = "";
            [JsonPropertyName("targetFileName")] public string TargetFileName { get; set; } = "";
        }

        public async Task<LegacyImportResult> ImportAsync(string manifestPath, string sourceDirectory, bool dryRun)
        {
            var result = new LegacyImportResult();

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("مانیفست ورود پیدا نشد.", manifestPath);
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"پوشه‌ی فایل‌های مبدأ پیدا نشد: {sourceDirectory}");

            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<ManifestRoot>(json)
                           ?? throw new InvalidOperationException("مانیفست قابل خواندن نیست.");
            result.Total = manifest.Records.Count;

            // مقصد فایل‌ها همان مسیری است که خودِ برنامه استفاده می‌کند، وگرنه دانلود سند
            // بعداً فایل را پیدا نمی‌کرد.
            var uploadPath = AppSettingFactory.AppSetting.UploadPath;
            if (string.IsNullOrWhiteSpace(uploadPath))
                throw new InvalidOperationException("UploadPath در تنظیمات مقدار ندارد.");
            Directory.CreateDirectory(uploadPath);

            // داده‌ی پایه یک‌بار خوانده می‌شود، نه به ازای هر رکورد.
            var managements = await _context.Set<OrganizationalManagement>()
                .ToDictionaryAsync(m => m.Code.ToUpperInvariant(), m => m.Key);
            // فعالیت زیرمجموعه‌ی مدیریت است و کدش بین مدیریت‌ها تکرار می‌شود (مثلاً GE در
            // هر ۱۴ مدیریت وجود دارد). پس کلید جستجو باید جفتِ «مدیریت + کد فعالیت» باشد؛
            // با کد تنها، سند به فعالیتِ مدیریتِ دیگری وصل می‌شد.
            var activities = await _context.Set<OrganizationalActivity>()
                .Select(a => new { a.Key, a.Code, a.OrganizationalManagementId })
                .ToListAsync();
            var activityByManagement = activities.ToDictionary(
                a => (a.OrganizationalManagementId, a.Code.ToUpperInvariant()),
                a => a.Key);
            var documentTypes = await _context.Set<DocumentType>()
                .ToDictionaryAsync(t => t.Code.ToUpperInvariant(), t => t.Key);

            var existingNumbers = await _context.Set<Document>()
                .Select(d => d.Number)
                .ToListAsync();
            var existing = new HashSet<string>(existingNumbers, StringComparer.OrdinalIgnoreCase);

            foreach (var record in manifest.Records)
            {
                // ایمنی در برابر اجرای دوباره: سند موجود دست نمی‌خورد.
                if (existing.Contains(record.Number))
                {
                    result.AlreadyExisted++;
                    continue;
                }

                if (!managements.TryGetValue(record.ManagementCode.ToUpperInvariant(), out var managementId))
                {
                    result.Failed++;
                    result.Problems.Add($"{record.Number}: مدیریت سازمانی '{record.ManagementCode}' پیدا نشد.");
                    continue;
                }

                if (!activityByManagement.TryGetValue(
                        (managementId, record.ActivityCode.ToUpperInvariant()), out var activityId))
                {
                    result.Failed++;
                    result.Problems.Add(
                        $"{record.Number}: فعالیت '{record.ActivityCode}' زیرمجموعه‌ی مدیریت '{record.ManagementCode}' نیست.");
                    continue;
                }

                if (!documentTypes.TryGetValue(record.DocumentTypeCode.ToUpperInvariant(), out var documentTypeId))
                {
                    result.Failed++;
                    result.Problems.Add($"{record.Number}: نوع سند '{record.DocumentTypeCode}' پیدا نشد.");
                    continue;
                }

                var source = Path.Combine(sourceDirectory, Path.GetFileName(record.SourceFile));
                if (!File.Exists(source))
                {
                    result.Failed++;
                    result.Problems.Add($"{record.Number}: فایل مبدأ نیست ({Path.GetFileName(record.SourceFile)}).");
                    continue;
                }

                if (dryRun)
                {
                    result.Inserted++;
                    existing.Add(record.Number);
                    continue;
                }

                try
                {
                    // اول فایل، بعد رکورد: اگر کپی شکست بخورد سندِ بی‌فایل ساخته نمی‌شود.
                    var destination = Path.Combine(uploadPath, record.TargetFileName);
                    File.Copy(source, destination, overwrite: true);

                    // متن فایل برای جستجو در محتوا - بهترین‌تلاش، شکستش مانع ورود نیست.
                    string? extractedText = null;
                    try
                    {
                        await using var stream = File.OpenRead(destination);
                        extractedText = _textExtraction.Extract(stream, record.TargetFileName);
                    }
                    catch
                    {
                        // نادیده: استخراج متن یک قابلیت جانبی است.
                    }

                    _context.Set<Document>().Add(new Document
                    {
                        Key = Guid.NewGuid(),
                        IsActive = true,
                        CreatedTime = DateTime.UtcNow,
                        CreatedByPCode = ImportPCode,

                        Number = record.Number,
                        Name = record.Name,
                        SerialNumber = record.SerialNumber,
                        LastVersion = (DocumentVersion)record.LastVersion,
                        // ستادی است، پس بازنگری مرکب پروژه‌ای ندارد.
                        ContentRevision = null,
                        IsEnglishVersion = record.IsEnglishVersion,
                        IsOutsideCodingStructure = record.IsOutsideCodingStructure,
                        Category = DocumentCategory.Headquarters,
                        ProjectId = null,

                        OrganizationalManagementId = managementId,
                        OrganizationalActivityId = activityId,
                        DocumentTypeId = documentTypeId,

                        FormerReviewDate = ParseDate(record.FormerReviewDate),
                        CurrentReviewDate = ParseDate(record.CurrentReviewDate),

                        FileName = record.TargetFileName,
                        ExtractedText = extractedText,
                    });

                    existing.Add(record.Number);
                    result.Inserted++;

                    // ذخیره‌ی دسته‌ای: تراکنش‌های کوچک‌تر یعنی شکستِ یک رکورد کل کار را
                    // برنمی‌گرداند و پیشرفت هم دیده می‌شود.
                    if (result.Inserted % 100 == 0)
                        await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Problems.Add($"{record.Number}: {ex.Message}");
                }
            }

            if (!dryRun)
                await _context.SaveChangesAsync();

            return result;
        }

        // تاریخ‌ها در مانیفست میلادی و به شکل yyyy-MM-dd هستند؛ تبدیل از شمسی قبلاً
        // هنگام ساخت مانیفست انجام شده.
        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var parsed) ? parsed : null;
        }
    }
}
