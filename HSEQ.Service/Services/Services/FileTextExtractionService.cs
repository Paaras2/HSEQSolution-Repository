using HSEQ.Common;
using HSEQ.Service.Interfaces.Services;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace HSEQ.Service.Services.Services
{
    // استخراج متن فایل برای «جستجو در محتوای فایل» - بدون هیچ پکیج جانبی:
    //   .txt و هم‌خانواده‌ها -> خواندن مستقیم
    //   .docx / .xlsx / .pptx -> فایل zip با XML استاندارد، با XDocument خوانده می‌شود
    //   .pdf                  -> PdfTextExtractor (تحلیل واقعیِ ساختار PDF)
    //   بقیه (تصویر و...)     -> نال
    //
    // خروجی همیشه از PersianText.Normalize رد می‌شود تا همان شکلی ذخیره شود که
    // SearchService عبارت جستجو را به آن تبدیل می‌کند؛ وگرنه «ي» عربیِ داخل فایل با
    // «ی» فارسیِ تایپ‌شده هرگز جور در نمی‌آمد.
    public class FileTextExtractionService : IFileTextExtractionService
    {
        // جلوگیری از حجم بیش‌ازحد ستون ExtractedText برای فایل‌های خیلی بزرگ.
        private const int MaxExtractedLength = 200_000;

        private static readonly XNamespace WordNamespace =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly XNamespace SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace DrawingNamespace =
            "http://schemas.openxmlformats.org/drawingml/2006/main";

        public string? Extract(Stream stream, string fileName)
        {
            try
            {
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                var text = extension switch
                {
                    ".txt" or ".csv" or ".md" or ".xml" or ".json" or ".htm" or ".html" => ExtractPlainText(stream),
                    ".docx" => ExtractWord(stream),
                    ".xlsx" => ExtractExcel(stream),
                    ".pptx" => ExtractPowerPoint(stream),
                    ".pdf" => ExtractPdf(stream),
                    _ => null,
                };

                if (string.IsNullOrWhiteSpace(text))
                    return null;

                var normalized = PersianText.Normalize(text);
                if (normalized.Length == 0)
                    return null;

                return normalized.Length > MaxExtractedLength ? normalized[..MaxExtractedLength] : normalized;
            }
            catch
            {
                // بهترین‌تلاش است - هیچ خطای پارس نباید بارگذاری مدرک را متوقف کند.
                return null;
            }
        }

        private static string ExtractPlainText(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return reader.ReadToEnd();
        }

        private static string? ExtractPdf(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            // سقف با ضریب اطمینان: یکسان‌سازیِ بعدی فقط متن را کوتاه‌تر می‌کند.
            return PdfTextExtractor.Extract(buffer.ToArray(), MaxExtractedLength * 2);
        }

        // ------------------------------------------------------------------
        // فرمت‌های آفیس - همگی zip حاوی XML
        // ------------------------------------------------------------------

        private static string? ExtractWord(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var builder = new StringBuilder();

            // سرصفحه و پاصفحه هم خوانده می‌شوند: شماره‌ی مدرک و نام شرکت معمولاً
            // آنجا هستند، نه در متن اصلی.
            foreach (var entry in archive.Entries)
            {
                var isBodyPart = entry.FullName == "word/document.xml"
                    || entry.FullName.StartsWith("word/header", StringComparison.Ordinal)
                    || entry.FullName.StartsWith("word/footer", StringComparison.Ordinal);

                if (!isBodyPart)
                    continue;

                using var entryStream = entry.Open();
                var document = XDocument.Load(entryStream);

                // پاراگراف واحدِ خط است: تکه‌های داخل یک پاراگراف بدون فاصله به هم
                // می‌چسبند (وگرنه یک واژه‌ی قالب‌بندی‌شده تکه‌تکه می‌شد) و بین
                // پاراگراف‌ها خط جدید می‌آید.
                foreach (var paragraph in document.Descendants(WordNamespace + "p"))
                {
                    foreach (var node in paragraph.Descendants())
                    {
                        if (node.Name == WordNamespace + "t")
                            builder.Append(node.Value);
                        else if (node.Name == WordNamespace + "tab" || node.Name == WordNamespace + "br")
                            builder.Append(' ');
                    }

                    builder.Append('\n');
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        private static string? ExtractExcel(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var builder = new StringBuilder();

            // در xlsx تقریباً همه‌ی متن‌ها در جدول رشته‌های مشترک نگهداری می‌شوند؛
            // sharedStrings تنها جایی است که باید خوانده شود.
            var shared = archive.GetEntry("xl/sharedStrings.xml");
            if (shared is not null)
            {
                using var entryStream = shared.Open();
                foreach (var text in XDocument.Load(entryStream).Descendants(SpreadsheetNamespace + "t"))
                    builder.Append(text.Value).Append('\n');
            }

            // رشته‌های درجا (inlineStr) که در جدول مشترک نیستند.
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal))
                    continue;

                using var entryStream = entry.Open();
                foreach (var inline in XDocument.Load(entryStream).Descendants(SpreadsheetNamespace + "is"))
                    builder.Append(inline.Value).Append('\n');
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        private static string? ExtractPowerPoint(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var builder = new StringBuilder();

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal))
                    continue;

                using var entryStream = entry.Open();
                foreach (var text in XDocument.Load(entryStream).Descendants(DrawingNamespace + "t"))
                    builder.Append(text.Value).Append('\n');
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
    }
}
