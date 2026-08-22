using HSEQ.Service.Interfaces.Services;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HSEQ.Service.Services.Services
{
    // پیاده‌سازی بدون هیچ پکیج جدید:
    //   .txt  -> خواندن مستقیم متن (پشتیبانی کامل، هر زبانی)
    //   .docx -> یک فایل zip است؛ متن از word/document.xml با XML استاندارد دات‌نت
    //            استخراج می‌شود (پشتیبانی کامل، فارسی هم مشکلی ندارد)
    //   .pdf  -> بهترین‌تلاش: فقط عملگرهای نمایش متن (Tj/TJ) داخل جریان‌های محتوا را
    //            می‌خواند. برای PDFهای اسکن‌شده یا با فونت فارسی جاسازی‌شده/CID، ممکن است
    //            متنی پیدا نکند - این یک محدودیت شناخته‌شده است (رفعش بدون یک کتابخانه‌ی
    //            واقعی PDF یا OCR ممکن نیست).
    //   بقیه‌ی فرمت‌ها (تصویر و...) -> نال
    public class FileTextExtractionService : IFileTextExtractionService
    {
        // جلوگیری از حجم بیش‌ازحد ستون ExtractedText برای فایل‌های خیلی بزرگ.
        private const int MaxExtractedLength = 200_000;

        public string? Extract(Stream stream, string fileName)
        {
            try
            {
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                var text = extension switch
                {
                    ".txt" => ExtractPlainText(stream),
                    ".docx" => ExtractDocx(stream),
                    ".pdf" => ExtractPdfBestEffort(stream),
                    _ => null,
                };

                if (string.IsNullOrWhiteSpace(text))
                    return null;

                text = text.Trim();
                return text.Length > MaxExtractedLength ? text[..MaxExtractedLength] : text;
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

        private static string? ExtractDocx(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
                return null;

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            return string.Join(" ", doc.Descendants(w + "t").Select(t => t.Value));
        }

        // NonBacktracking حیاتی است: در ShowTextArrayPattern کلاس [^\[\]] بک‌اسلش را
        // استثنا نمی‌کند و با شاخه‌ی \\. هم‌پوشان است؛ روی بایت‌های باینری PDF این ابهام به
        // backtracking نمایی می‌رسید و ذخیره‌ی یک PDF یک‌مگابایتی عملاً هرگز تمام نمی‌شد.
        // موتور NonBacktracking زمان خطی را تضمین می‌کند.
        private static readonly Regex StreamBlockPattern =
            new(@"stream\r?\n(?<data>.*?)\r?\nendstream", RegexOptions.Singleline | RegexOptions.NonBacktracking);
        private static readonly Regex ShowTextPattern =
            new(@"\((?<text>(?:[^()\\]|\\.)*)\)\s*Tj", RegexOptions.NonBacktracking);
        private static readonly Regex ShowTextArrayPattern =
            new(@"\[(?<arr>(?:[^\[\]]|\\.)*)\]\s*TJ", RegexOptions.NonBacktracking);
        private static readonly Regex ArrayStringPattern =
            new(@"\((?<text>(?:[^()\\]|\\.)*)\)", RegexOptions.NonBacktracking);

        private static string? ExtractPdfBestEffort(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            // Latin1 یک نگاشت یک‌به‌یک بایت<->کاراکتر است - لازم برای پیدا کردن مارکرهای
            // متنی PDF (stream/endstream/Tj/TJ) بدون خراب‌کردن بایت‌های خام جریان فشرده.
            var raw = Encoding.Latin1.GetString(buffer.ToArray());

            var result = new StringBuilder();
            foreach (Match streamMatch in StreamBlockPattern.Matches(raw))
            {
                // بعد از رسیدن به سقف MaxExtractedLength ادامه‌ی اسکن فقط دورریز می‌شود.
                if (result.Length >= MaxExtractedLength)
                    break;

                var content = DecompressIfNeeded(streamMatch.Groups["data"].Value);
                AppendShownText(content, result);
            }

            return result.Length > 0 ? result.ToString() : null;
        }

        private static string DecompressIfNeeded(string rawStreamData)
        {
            // هدر zlib همیشه با 0x78 شروع می‌شود؛ بدون آن، تلاش برای بازکردن فقط یک استثنای
            // پرهزینه به ازای هر بلوک بود - جریان غیرفشرده مستقیم برگردانده می‌شود.
            if (rawStreamData.Length == 0 || rawStreamData[0] != '\x78')
                return rawStreamData;

            try
            {
                var dataBytes = Encoding.Latin1.GetBytes(rawStreamData);
                using var compressed = new MemoryStream(dataBytes);
                using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                using var reader = new StreamReader(zlib, Encoding.Latin1);
                return reader.ReadToEnd();
            }
            catch
            {
                // جریان از قبل فشرده نبوده (یا فرمت فشرده‌سازی دیگری دارد) - متن خام را برگردان.
                return rawStreamData;
            }
        }

        private static void AppendShownText(string content, StringBuilder result)
        {
            foreach (Match m in ShowTextPattern.Matches(content))
                result.Append(UnescapePdfString(m.Groups["text"].Value)).Append(' ');

            foreach (Match arrayMatch in ShowTextArrayPattern.Matches(content))
                foreach (Match m in ArrayStringPattern.Matches(arrayMatch.Groups["arr"].Value))
                    result.Append(UnescapePdfString(m.Groups["text"].Value)).Append(' ');
        }

        private static string UnescapePdfString(string s) =>
            s.Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\");
    }
}
