using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace HSEQ.Service.Services.Services
{
    // استخراج متن از PDF - بدون هیچ پکیج جانبی.
    //
    // نسخه‌ی قبلی فقط رشته‌های داخل پرانتزِ جریان‌های محتوا را می‌خواند و بایت‌ها را
    // Latin1 حساب می‌کرد. برای PDFهای فارسیِ این آرشیو نتیجه‌اش صفر بود، چون:
    //   ۱) فونت‌ها Type0/CID هستند و رشته‌ها به شکل هگز <....> نوشته می‌شوند نه (....)
    //   ۲) کدِ هر گلیف فقط از راه جدول /ToUnicode به یونیکد ترجمه می‌شود
    //   ۳) خودِ آن جدول‌ها معمولاً داخل «جریان اشیاء» (/ObjStm) فشرده‌اند و بدون باز
    //      کردن آن‌ها اصلاً دیده نمی‌شوند
    //   ۴) متن فارسی در PDF به ترتیب دیداری (راست‌به‌چپ) ذخیره می‌شود، پس بدون
    //      بازچینش، «شماره مدرک» به شکل «كردم هرامش» در دیتابیس می‌نشست
    //
    // این پیاده‌سازی هر چهار مورد را انجام می‌دهد. آنچه هنوز از دستش برمی‌آید نیست،
    // PDF اسکن‌شده است (تصویرِ کاغذ، بدون لایه‌ی متنی) که بدون OCR قابل خواندن نیست.
    internal static class PdfTextExtractor
    {
        // متن با Latin1 خوانده می‌شود چون نگاشت یک‌به‌یکِ بایت<->کاراکتر است: هم
        // نشانه‌های ساختاری PDF درست پیدا می‌شوند و هم بایت‌های خام دست‌نخورده می‌مانند.
        private static readonly Encoding Bytes = Encoding.Latin1;

        // سقف‌های محافظتی در برابر فایل خراب یا ساختگی.
        private const int MaxCMapEntries = 200_000;
        private const int MaxPages = 2_000;

        // «(\d+) \d+ obj» - نگاه‌به‌عقب جلوی این را می‌گیرد که وسط یک عدد بلند شروع کند.
        private static readonly Regex ObjectHeaderPattern =
            new(@"(?<![0-9])(\d+)\s+\d+\s+obj\b", RegexOptions.Compiled);

        private static readonly Regex StreamStartPattern =
            new(@"stream\r?\n", RegexOptions.Compiled);

        // «/Type /Page» ولی نه «/Type /Pages» - گره‌های میانیِ درخت صفحات محتوا ندارند.
        private static readonly Regex PageObjectPattern =
            new(@"/Type\s*/Page(?![a-zA-Z])", RegexOptions.Compiled);

        private static readonly Regex FontEntryPattern =
            new(@"/([A-Za-z0-9#+.\-]+)\s+(\d+)\s+\d+\s*R", RegexOptions.Compiled);

        private static readonly Regex ToUnicodeRefPattern =
            new(@"/ToUnicode\s+(\d+)\s+\d+\s*R", RegexOptions.Compiled);

        public static string? Extract(byte[] content, int maxLength)
        {
            var raw = Bytes.GetString(content);
            var objects = IndexObjects(raw);
            if (objects.Count == 0)
                return null;

            var builder = new StringBuilder();
            var pageCount = 0;

            // ترتیب شماره‌ی شیء تقریبِ خوبی از ترتیب صفحات است؛ برای جستجو کافی است.
            foreach (var number in objects.Keys.OrderBy(k => k))
            {
                if (builder.Length >= maxLength || pageCount >= MaxPages)
                    break;

                var body = objects[number];
                if (!PageObjectPattern.IsMatch(body))
                    continue;

                pageCount++;
                var page = ExtractPage(objects, body);
                if (page.Length > 0)
                    builder.Append(page).Append('\n');
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        // ------------------------------------------------------------------
        // نمایه‌ی اشیاء
        // ------------------------------------------------------------------

        // شماره‌ی شیء -> بدنه‌ی خام آن. اشیای داخل /ObjStm هم باز و به همین نمایه اضافه
        // می‌شوند، وگرنه جدول‌های /ToUnicode اصلاً پیدا نمی‌شدند.
        private static Dictionary<int, string> IndexObjects(string raw)
        {
            var objects = new Dictionary<int, string>();

            foreach (Match match in ObjectHeaderPattern.Matches(raw))
            {
                if (!int.TryParse(match.Groups[1].Value, out var number))
                    continue;

                var start = match.Index + match.Length;
                var end = raw.IndexOf("endobj", start, StringComparison.Ordinal);
                // بازنویسیِ عمدی: در PDFهای «به‌روزرسانی افزایشی»، تعریف بعدیِ یک شماره
                // همان نسخه‌ی معتبر است.
                objects[number] = end > start ? raw[start..end] : raw[start..];
            }

            ExpandObjectStreams(objects);
            return objects;
        }

        private static void ExpandObjectStreams(Dictionary<int, string> objects)
        {
            var nested = new Dictionary<int, string>();

            foreach (var body in objects.Values)
            {
                if (!body.Contains("/ObjStm", StringComparison.Ordinal))
                    continue;

                var data = ReadStream(body);
                if (data is null)
                    continue;

                var countMatch = Regex.Match(body, @"/N\s+(\d+)");
                var firstMatch = Regex.Match(body, @"/First\s+(\d+)");
                if (!countMatch.Success || !firstMatch.Success)
                    continue;

                var count = int.Parse(countMatch.Groups[1].Value);
                var first = int.Parse(firstMatch.Groups[1].Value);
                if (first < 0 || first > data.Length)
                    continue;

                // سرآیند = دنباله‌ی «شماره‌ی شیء، آفست» برای N شیء.
                var header = data[..first].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < count && (2 * i) + 1 < header.Length; i++)
                {
                    if (!int.TryParse(header[2 * i], out var objectNumber) ||
                        !int.TryParse(header[(2 * i) + 1], out var offset))
                        break;

                    var start = first + offset;
                    var end = data.Length;
                    if ((2 * i) + 3 < header.Length && int.TryParse(header[(2 * i) + 3], out var nextOffset))
                        end = Math.Min(data.Length, first + nextOffset);

                    if (start >= 0 && start < end)
                        nested[objectNumber] = data[start..end];
                }
            }

            // اشیای بیرونی اولویت دارند - نسخه‌ی به‌روزرسانی‌شده همان‌هاست.
            foreach (var pair in nested)
                objects.TryAdd(pair.Key, pair.Value);
        }

        // محتوای یک شیءِ جریان‌دار، در صورت لزوم از حالت فشرده خارج‌شده.
        private static string? ReadStream(string body)
        {
            var match = StreamStartPattern.Match(body);
            if (!match.Success)
                return null;

            var dictionary = body[..match.Index];
            var payloadStart = match.Index + match.Length;
            var payloadEnd = body.LastIndexOf("endstream", StringComparison.Ordinal);
            if (payloadEnd <= payloadStart)
                payloadEnd = body.Length;

            var payload = body[payloadStart..payloadEnd].TrimEnd('\r', '\n');
            if (payload.Length == 0)
                return null;

            if (!dictionary.Contains("/FlateDecode", StringComparison.Ordinal))
                return payload;

            var inflated = Inflate(Bytes.GetBytes(payload));
            return inflated is null ? null : Bytes.GetString(inflated);
        }

        private static byte[]? Inflate(byte[] data)
        {
            try
            {
                using var source = new MemoryStream(data);
                using var zlib = new ZLibStream(source, CompressionMode.Decompress);
                using var target = new MemoryStream();
                zlib.CopyTo(target);
                return target.ToArray();
            }
            catch
            {
                // جریان‌های ناقص: هر چه تا لحظه‌ی خطا باز شده بهتر از هیچ است، ولی
                // ZLibStream خروجی جزئی نمی‌دهد - پس اینجا فقط تسلیم می‌شویم.
                return null;
            }
        }

        // ------------------------------------------------------------------
        // یک صفحه: فونت‌ها + جریان محتوا
        // ------------------------------------------------------------------

        private static string ExtractPage(Dictionary<int, string> objects, string page)
        {
            var resources = ResolveEntry(objects, page, "/Resources");
            var fontDictionary = ResolveEntry(objects, resources, "/Font");
            var fonts = LoadFonts(objects, fontDictionary);
            var content = ReadPageContent(objects, page);

            return content.Length == 0 ? string.Empty : Layout(DecodeContent(content, fonts));
        }

        // مقدار یک کلید که هم می‌تواند ارجاع غیرمستقیم («12 0 R») باشد و هم دیکشنری درجا.
        private static string ResolveEntry(Dictionary<int, string> objects, string container, string key)
        {
            if (container.Length == 0)
                return string.Empty;

            var match = Regex.Match(container, Regex.Escape(key) + @"\s*(?:(\d+)\s+\d+\s*R|(<<))");
            if (!match.Success)
                return string.Empty;

            if (match.Groups[1].Success)
                return objects.TryGetValue(int.Parse(match.Groups[1].Value), out var referenced) ? referenced : string.Empty;

            return ReadDictionary(container, match.Groups[2].Index);
        }

        // دیکشنریِ تودرتو را با شمردن << و >> برمی‌دارد؛ با Regex نمی‌شود چون تودرتوست.
        private static string ReadDictionary(string buffer, int start)
        {
            var depth = 0;
            var index = start;

            while (index < buffer.Length - 1)
            {
                if (buffer[index] == '<' && buffer[index + 1] == '<')
                {
                    depth++;
                    index += 2;
                    continue;
                }

                if (buffer[index] == '>' && buffer[index + 1] == '>')
                {
                    depth--;
                    index += 2;
                    if (depth == 0)
                        return buffer[start..index];
                    continue;
                }

                index++;
            }

            return buffer[start..];
        }

        private sealed class PdfFont
        {
            // کدِ گلیف -> متن یونیکد. خالی یعنی فونت جدول ترجمه ندارد.
            public Dictionary<int, string> ToUnicode { get; init; } = new();

            // فونت‌های Type0 کدهای دوبایتی دارند (عملاً همیشه Identity-H).
            public bool TwoByte { get; init; }
        }

        private static Dictionary<string, PdfFont> LoadFonts(Dictionary<int, string> objects, string fontDictionary)
        {
            var fonts = new Dictionary<string, PdfFont>(StringComparer.Ordinal);
            if (fontDictionary.Length == 0)
                return fonts;

            foreach (Match entry in FontEntryPattern.Matches(fontDictionary))
            {
                var name = entry.Groups[1].Value;
                if (fonts.ContainsKey(name))
                    continue;

                if (!objects.TryGetValue(int.Parse(entry.Groups[2].Value), out var font))
                    continue;

                var map = new Dictionary<int, string>();
                var toUnicode = ToUnicodeRefPattern.Match(font);
                if (toUnicode.Success && objects.TryGetValue(int.Parse(toUnicode.Groups[1].Value), out var cmapObject))
                {
                    var cmap = ReadStream(cmapObject);
                    if (cmap is not null)
                        map = ParseToUnicode(cmap);
                }

                fonts[name] = new PdfFont
                {
                    ToUnicode = map,
                    TwoByte = font.Contains("/Type0", StringComparison.Ordinal),
                };
            }

            return fonts;
        }

        private static string ReadPageContent(Dictionary<int, string> objects, string page)
        {
            var match = Regex.Match(page, @"/Contents\s*(?:(\d+)\s+\d+\s*R|(\[))");
            if (!match.Success)
                return string.Empty;

            if (match.Groups[1].Success)
            {
                objects.TryGetValue(int.Parse(match.Groups[1].Value), out var single);
                return (single is null ? null : ReadStream(single)) ?? string.Empty;
            }

            // آرایه‌ای از جریان‌ها: صفحه‌ای که محتوایش تکه‌تکه ذخیره شده.
            var arrayEnd = page.IndexOf(']', match.Groups[2].Index);
            if (arrayEnd < 0)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (Match reference in Regex.Matches(page[match.Groups[2].Index..arrayEnd], @"(\d+)\s+\d+\s*R"))
            {
                if (!objects.TryGetValue(int.Parse(reference.Groups[1].Value), out var part))
                    continue;

                var data = ReadStream(part);
                if (data is not null)
                    builder.Append(data).Append('\n');
            }

            return builder.ToString();
        }

        // ------------------------------------------------------------------
        // جدول /ToUnicode
        // ------------------------------------------------------------------

        private static readonly Regex BfCharPattern =
            new(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]*)>", RegexOptions.Compiled);

        private static readonly Regex BfRangePattern =
            new(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(?:<([0-9A-Fa-f]*)>|\[([^\]]*)\])", RegexOptions.Compiled);

        private static Dictionary<int, string> ParseToUnicode(string cmap)
        {
            var map = new Dictionary<int, string>();

            foreach (var block in Blocks(cmap, "beginbfchar", "endbfchar"))
            {
                foreach (Match entry in BfCharPattern.Matches(block))
                {
                    if (map.Count >= MaxCMapEntries) return map;
                    map[Convert.ToInt32(entry.Groups[1].Value, 16)] = FromUtf16Hex(entry.Groups[2].Value);
                }
            }

            foreach (var block in Blocks(cmap, "beginbfrange", "endbfrange"))
            {
                foreach (Match entry in BfRangePattern.Matches(block))
                {
                    var from = Convert.ToInt32(entry.Groups[1].Value, 16);
                    var to = Convert.ToInt32(entry.Groups[2].Value, 16);

                    // شکل اول: یک مقصد که برای کل بازه یکی‌یکی جلو می‌رود.
                    if (entry.Groups[3].Success)
                    {
                        var start = entry.Groups[3].Value;
                        if (start.Length == 0) continue;
                        var baseValue = Convert.ToInt32(start, 16);

                        for (var offset = 0; offset <= to - from; offset++)
                        {
                            if (map.Count >= MaxCMapEntries) return map;
                            var code = baseValue + offset;
                            if (code is < 0 or > 0xFFFF) break;
                            map[from + offset] = ((char)code).ToString();
                        }
                        continue;
                    }

                    // شکل دوم: آرایه‌ای که برای هر کد مقصد جداگانه می‌دهد.
                    var index = 0;
                    foreach (Match destination in Regex.Matches(entry.Groups[4].Value, @"<([0-9A-Fa-f]*)>"))
                    {
                        if (map.Count >= MaxCMapEntries) return map;
                        map[from + index] = FromUtf16Hex(destination.Groups[1].Value);
                        index++;
                    }
                }
            }

            return map;
        }

        // بلوک‌های بین یک جفت نشانه‌ی آغاز/پایان؛ با IndexOf نه Regex، تا روی فایل‌های
        // بزرگ هزینه‌ی پویشِ تنبل تکرار نشود.
        private static IEnumerable<string> Blocks(string source, string open, string close)
        {
            var index = 0;
            while (true)
            {
                var start = source.IndexOf(open, index, StringComparison.Ordinal);
                if (start < 0) yield break;

                start += open.Length;
                var end = source.IndexOf(close, start, StringComparison.Ordinal);
                if (end < 0) yield break;

                yield return source[start..end];
                index = end + close.Length;
            }
        }

        private static string FromUtf16Hex(string hex)
        {
            if (hex.Length < 4)
                return string.Empty;

            var builder = new StringBuilder(hex.Length / 4);
            for (var i = 0; i + 4 <= hex.Length; i += 4)
                builder.Append((char)Convert.ToInt32(hex.Substring(i, 4), 16));

            return builder.ToString();
        }

        // ------------------------------------------------------------------
        // خواندن جریان محتوا
        // ------------------------------------------------------------------

        // یک تکه متن به همراه جایی که روی صفحه نشسته - مختصات برای بازچینش لازم است.
        private readonly record struct PlacedText(double X, double Y, string Text);

        private enum TokenKind { Number, Name, Text }

        private readonly record struct Token(TokenKind Kind, double Number, string Value);

        private static List<PlacedText> DecodeContent(string content, Dictionary<string, PdfFont> fonts)
        {
            var placed = new List<PlacedText>();
            var segment = new StringBuilder();
            var operands = new List<Token>();

            PdfFont? font = null;
            double x = 0, y = 0, leading = 0;
            double segmentX = 0, segmentY = 0;
            var index = 0;

            // تکه‌ی جاری را با مختصاتی که در آن شروع شده ثبت می‌کند.
            void Flush()
            {
                if (segment.Length == 0)
                    return;

                placed.Add(new PlacedText(segmentX, segmentY, Reorder(segment.ToString())));
                segment.Clear();
            }

            void Append(string text)
            {
                if (text.Length == 0)
                    return;

                if (segment.Length == 0)
                {
                    segmentX = x;
                    segmentY = y;
                }

                segment.Append(text);
            }

            while (index < content.Length)
            {
                var ch = content[index];

                if (char.IsWhiteSpace(ch)) { index++; continue; }

                // توضیحات تا آخر خط
                if (ch == '%')
                {
                    while (index < content.Length && content[index] is not ('\r' or '\n')) index++;
                    continue;
                }

                if (ch == '(')
                {
                    operands.Add(new Token(TokenKind.Text, 0, ReadLiteralString(content, ref index)));
                    continue;
                }

                if (ch == '<')
                {
                    // «<<» آغاز دیکشنری است نه رشته‌ی هگز.
                    if (index + 1 < content.Length && content[index + 1] == '<') { index += 2; continue; }
                    operands.Add(new Token(TokenKind.Text, 0, ReadHexString(content, ref index)));
                    continue;
                }

                if (ch == '>') { index += content.Length > index + 1 && content[index + 1] == '>' ? 2 : 1; continue; }
                if (ch is '[' or ']' or '{' or '}') { index++; continue; }

                if (ch == '/')
                {
                    operands.Add(new Token(TokenKind.Name, 0, ReadName(content, ref index)));
                    continue;
                }

                if (ch is >= '0' and <= '9' or '+' or '-' or '.')
                {
                    operands.Add(new Token(TokenKind.Number, ReadNumber(content, ref index), string.Empty));
                    continue;
                }

                var op = ReadOperator(content, ref index);
                if (op.Length == 0) { index++; continue; }

                switch (op)
                {
                    case "Tf":
                        var name = LastName(operands);
                        font = name is not null && fonts.TryGetValue(name, out var selected) ? selected : null;
                        break;

                    // نمایش متن. «'» و «"» اول به خط بعد می‌روند.
                    case "\"":
                    case "'":
                        Flush();
                        y -= leading;
                        Append(Decode(LastText(operands), font));
                        break;

                    case "Tj":
                        Append(Decode(LastText(operands), font));
                        break;

                    // آرایه‌ی TJ: رشته‌ها پشت‌سرهم‌اند و عددهای منفیِ بزرگ یعنی فاصله‌ی کلمه.
                    // بدون این تشخیص، «Engineering Execution Plan» به هم می‌چسبید یا -
                    // مثل نسخه‌ی قبلی که بین هر تکه فاصله می‌گذاشت - به «Engineer ing
                    // E x ecut ion» تکه‌تکه می‌شد و جستجوی عبارت هرگز جواب نمی‌داد.
                    case "TJ":
                        foreach (var operand in operands)
                        {
                            if (operand.Kind == TokenKind.Text)
                                Append(Decode(operand.Value, font));
                            else if (operand.Kind == TokenKind.Number && operand.Number <= -120 &&
                                     segment.Length > 0 && segment[^1] != ' ')
                                segment.Append(' ');
                        }
                        break;

                    case "Tm":
                        Flush();
                        if (operands.Count >= 6)
                        {
                            x = operands[^2].Number;
                            y = operands[^1].Number;
                        }
                        break;

                    case "TD":
                        Flush();
                        if (operands.Count >= 2)
                        {
                            leading = -operands[^1].Number;
                            x += operands[^2].Number;
                            y += operands[^1].Number;
                        }
                        break;

                    case "Td":
                        Flush();
                        if (operands.Count >= 2)
                        {
                            x += operands[^2].Number;
                            y += operands[^1].Number;
                        }
                        break;

                    case "T*":
                        Flush();
                        y -= leading;
                        break;

                    case "TL":
                        if (operands.Count >= 1) leading = operands[^1].Number;
                        break;

                    case "BT":
                        Flush();
                        x = 0;
                        y = 0;
                        break;

                    case "ET":
                        Flush();
                        break;

                    // تصویر درجا: بایت‌های خامش پر از پرانتز است و اگر خوانده شود، آشغال
                    // وارد متن می‌کند. تا EI رد می‌شود.
                    case "BI":
                        var imageEnd = content.IndexOf("EI", index, StringComparison.Ordinal);
                        index = imageEnd < 0 ? content.Length : imageEnd + 2;
                        break;
                }

                operands.Clear();
            }

            Flush();
            return placed;
        }

        private static string? LastName(List<Token> operands)
        {
            for (var i = operands.Count - 1; i >= 0; i--)
                if (operands[i].Kind == TokenKind.Name)
                    return operands[i].Value;

            return null;
        }

        private static string LastText(List<Token> operands)
        {
            for (var i = operands.Count - 1; i >= 0; i--)
                if (operands[i].Kind == TokenKind.Text)
                    return operands[i].Value;

            return string.Empty;
        }

        // ------------------------------------------------------------------
        // خواندن نشانه‌ها
        // ------------------------------------------------------------------

        private static string ReadLiteralString(string content, ref int index)
        {
            index++; // از روی '('
            var builder = new StringBuilder();
            var depth = 1;

            while (index < content.Length)
            {
                var ch = content[index];

                if (ch == '\\')
                {
                    index++;
                    if (index >= content.Length) break;

                    var escaped = content[index];
                    switch (escaped)
                    {
                        case 'n': builder.Append('\n'); index++; break;
                        case 'r': builder.Append('\r'); index++; break;
                        case 't': builder.Append('\t'); index++; break;
                        case 'b': case 'f': builder.Append(' '); index++; break;
                        case '\r':
                            index++;
                            if (index < content.Length && content[index] == '\n') index++;
                            break;
                        case '\n': index++; break;
                        default:
                            // \ddd هشت‌هشتی
                            if (escaped is >= '0' and <= '7')
                            {
                                var value = 0;
                                var digits = 0;
                                while (index < content.Length && digits < 3 && content[index] is >= '0' and <= '7')
                                {
                                    value = (value * 8) + (content[index] - '0');
                                    index++;
                                    digits++;
                                }
                                builder.Append((char)(value & 0xFF));
                            }
                            else
                            {
                                builder.Append(escaped);
                                index++;
                            }
                            break;
                    }
                    continue;
                }

                if (ch == '(') depth++;
                if (ch == ')')
                {
                    depth--;
                    if (depth == 0) { index++; break; }
                }

                builder.Append(ch);
                index++;
            }

            return builder.ToString();
        }

        private static string ReadHexString(string content, ref int index)
        {
            index++; // از روی '<'
            var digits = new StringBuilder();

            while (index < content.Length && content[index] != '>')
            {
                var ch = content[index];
                if (Uri.IsHexDigit(ch)) digits.Append(ch);
                index++;
            }

            if (index < content.Length) index++; // از روی '>'

            // تعداد فردِ رقم: طبق استاندارد، رقم آخر با صفر کامل می‌شود.
            if (digits.Length % 2 == 1) digits.Append('0');

            var builder = new StringBuilder(digits.Length / 2);
            for (var i = 0; i + 2 <= digits.Length; i += 2)
                builder.Append((char)Convert.ToInt32(digits.ToString(i, 2), 16));

            return builder.ToString();
        }

        private static string ReadName(string content, ref int index)
        {
            index++; // از روی '/'
            var start = index;

            while (index < content.Length && !IsDelimiter(content[index]) && !char.IsWhiteSpace(content[index]))
                index++;

            return content[start..index];
        }

        private static double ReadNumber(string content, ref int index)
        {
            var start = index;
            if (content[index] is '+' or '-') index++;

            while (index < content.Length && (content[index] is >= '0' and <= '9' or '.'))
                index++;

            return double.TryParse(content[start..index],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value) ? value : 0;
        }

        private static string ReadOperator(string content, ref int index)
        {
            var start = index;

            while (index < content.Length && !IsDelimiter(content[index]) && !char.IsWhiteSpace(content[index]))
                index++;

            return content[start..index];
        }

        private static bool IsDelimiter(char ch) =>
            ch is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';

        // ------------------------------------------------------------------
        // ترجمه‌ی کد گلیف به متن
        // ------------------------------------------------------------------

        private static string Decode(string raw, PdfFont? font)
        {
            if (raw.Length == 0)
                return string.Empty;

            var builder = new StringBuilder(raw.Length);

            // Type0: هر دو بایت یک کد.
            if (font is { TwoByte: true })
            {
                for (var i = 0; i + 1 < raw.Length; i += 2)
                {
                    var code = (raw[i] << 8) | raw[i + 1];
                    if (font.ToUnicode.TryGetValue(code, out var mapped))
                        builder.Append(mapped);
                }

                return builder.ToString();
            }

            // فونت‌های ساده: جدول ترجمه اگر بود، وگرنه بایت را ASCII حساب می‌کنیم.
            foreach (var ch in raw)
            {
                if (font is not null && font.ToUnicode.TryGetValue(ch, out var mapped))
                    builder.Append(mapped);
                else if (ch is >= ' ' and < (char)127)
                    builder.Append(ch);
                else if (ch is '\n' or '\r' or '\t')
                    builder.Append(' ');
            }

            return builder.ToString();
        }

        // ------------------------------------------------------------------
        // بازچینش دیداری -> منطقی
        // ------------------------------------------------------------------

        private static bool IsRightToLeft(char ch) =>
            ch is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFF';

        private static bool ContainsRightToLeft(string text)
        {
            foreach (var ch in text)
                if (IsRightToLeft(ch))
                    return true;

            return false;
        }

        // متن راست‌به‌چپ در PDF به ترتیب دیداری نوشته می‌شود؛ برعکس کردنش ترتیب منطقی
        // (همان چیزی که کاربر تایپ می‌کند) را برمی‌گرداند. تکه‌های لاتین/عددی داخلش
        // نباید برعکس شوند، وگرنه «AGECH-001-B» به «B-100-HCEGA» تبدیل می‌شد.
        private static string Reorder(string text)
        {
            if (!ContainsRightToLeft(text))
                return text;

            var runs = new List<(bool IsLatin, string Value)>();
            var buffer = new StringBuilder();
            bool? currentIsLatin = null;

            foreach (var ch in text)
            {
                var isLatin = char.IsAsciiLetterOrDigit(ch);
                if (currentIsLatin is null || isLatin == currentIsLatin)
                {
                    buffer.Append(ch);
                    currentIsLatin = isLatin;
                    continue;
                }

                runs.Add((currentIsLatin.Value, buffer.ToString()));
                buffer.Clear().Append(ch);
                currentIsLatin = isLatin;
            }

            if (buffer.Length > 0 && currentIsLatin is not null)
                runs.Add((currentIsLatin.Value, buffer.ToString()));

            var result = new StringBuilder(text.Length);
            for (var i = runs.Count - 1; i >= 0; i--)
            {
                var run = runs[i];
                if (run.IsLatin)
                {
                    result.Append(run.Value);
                    continue;
                }

                for (var j = run.Value.Length - 1; j >= 0; j--)
                    result.Append(run.Value[j]);
            }

            return result.ToString();
        }

        // تکه‌ها بر اساس مختصات عمودی خط‌بندی می‌شوند و داخل هر خط به ترتیب خواندن
        // مرتب. بدون این کار، کلمه‌های یک جمله‌ی فارسی وارونه کنار هم می‌نشستند و
        // جستجوی عبارت («شماره مدرک») هیچ‌وقت جواب نمی‌داد.
        private static string Layout(List<PlacedText> placed)
        {
            if (placed.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();

            var lines = placed
                .GroupBy(p => Math.Round(p.Y))
                .OrderByDescending(g => g.Key);

            foreach (var line in lines)
            {
                var isRightToLeft = line.Any(p => ContainsRightToLeft(p.Text));
                var ordered = isRightToLeft
                    ? line.OrderByDescending(p => p.X)
                    : line.OrderBy(p => p.X);

                builder.AppendJoin(' ', ordered.Select(p => p.Text)).Append('\n');
            }

            return builder.ToString();
        }
    }
}
