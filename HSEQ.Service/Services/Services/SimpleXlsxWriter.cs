using System.IO.Compression;
using System.Text;

namespace HSEQ.Service.Services.Services
{
    // نویسنده‌ی حداقلی و بدون هیچ پکیج جدید برای فایل .xlsx واقعی (نه CSV) - فقط با
    // System.IO.Compression و رشته‌های XML دستی، طبق ساختار استاندارد OOXML. برای خروجی
    // اکسل جستجوی پیشرفته استفاده می‌شود؛ اگر نیاز به قالب‌بندی/فرمول بود باید یک کتابخانه‌ی
    // واقعی (ClosedXML/EPPlus) اضافه شود - این فقط داده‌ی خام را با هدر می‌نویسد.
    public static class SimpleXlsxWriter
    {
        public static byte[] Write(string sheetName, string[] headers, IEnumerable<string[]> rows)
        {
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
                WriteEntry(archive, "_rels/.rels", RootRelsXml());
                WriteEntry(archive, "xl/workbook.xml", WorkbookXml(XmlEscape(sheetName)));
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
                WriteEntry(archive, "xl/worksheets/sheet1.xml", SheetXml(headers, rows));
            }

            return output.ToArray();
        }

        private static void WriteEntry(ZipArchive archive, string path, string xmlContent)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(xmlContent);
            entryStream.Write(bytes, 0, bytes.Length);
        }

        private static string ContentTypesXml() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "</Types>";

        private static string RootRelsXml() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static string WorkbookXml(string sheetName) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            $"<sheets><sheet name=\"{sheetName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        private static string WorkbookRelsXml() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "</Relationships>";

        private static string SheetXml(string[] headers, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            AppendRow(sb, 1, headers);

            var rowIndex = 2;
            foreach (var row in rows)
            {
                AppendRow(sb, rowIndex, row);
                rowIndex++;
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, int rowNumber, string[] cells)
        {
            sb.Append($"<row r=\"{rowNumber}\">");
            for (var col = 0; col < cells.Length; col++)
            {
                var reference = ColumnLetter(col + 1) + rowNumber;
                var value = XmlEscape(cells[col] ?? string.Empty);
                sb.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{value}</t></is></c>");
            }
            sb.Append("</row>");
        }

        // تبدیل شماره‌ی ستون (پایه ۱) به حروف اکسل: 1->A, 26->Z, 27->AA ...
        private static string ColumnLetter(int columnNumber)
        {
            var letters = string.Empty;
            while (columnNumber > 0)
            {
                var remainder = (columnNumber - 1) % 26;
                letters = (char)('A' + remainder) + letters;
                columnNumber = (columnNumber - 1) / 26;
            }
            return letters;
        }

        private static string XmlEscape(string value) =>
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
