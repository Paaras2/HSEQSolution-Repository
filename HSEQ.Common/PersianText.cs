using System.Text;

namespace HSEQ.Common
{
    // یکسان‌سازی متن فارسی/عربی برای جستجو.
    //
    // چرا لازم است: کولیشن دیتابیس (SQL_Latin1_General_CP1_CI_AS) «ي» عربی و «ی» فارسی
    // را دو حرف متفاوت می‌بیند، و متنی که از PDF بیرون می‌آید پر از اشکال نمایشیِ
    // (Presentation Forms) حروف عربی است. بدون این یکسان‌سازی، عبارتی که کاربر تایپ
    // می‌کند هرگز با متن استخراج‌شده جور در نمی‌آید.
    //
    // هم روی متنِ ذخیره‌شده اعمال می‌شود و هم روی عبارت جستجو، تا دو طرفِ مقایسه
    // همیشه یک شکل داشته باشند.
    public static class PersianText
    {
        // بی‌اثر روی رشته‌ی خالی - فراخوان‌ها لازم نیست جداگانه چک کنند.
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // FormKC اشکال نمایشیِ عربی (FB50..FEFF) را به حرف پایه برمی‌گرداند و
            // لیگاتورها را باز می‌کند؛ متن استخراج‌شده از PDF دقیقاً همین شکل را دارد.
            var text = SafeNormalize(value);

            var builder = new StringBuilder(text.Length);
            var lastWasSpace = true; // برای اینکه فاصله‌ی ابتدای رشته نوشته نشود

            foreach (var ch in text)
            {
                var mapped = Map(ch);

                if (mapped == Drop)
                    continue;

                if (mapped == ' ')
                {
                    if (lastWasSpace) continue;
                    builder.Append(' ');
                    lastWasSpace = true;
                    continue;
                }

                builder.Append(mapped);
                lastWasSpace = false;
            }

            // فاصله‌ی انتهایی که ممکن است باقی مانده باشد.
            if (builder.Length > 0 && builder[^1] == ' ')
                builder.Length--;

            return builder.ToString();
        }

        // برخی رشته‌های خراب (بایت‌های نیمه‌جفتِ سروگیت از فایل‌های معیوب) Normalize را
        // می‌شکنند؛ در آن حالت متن خام کافی است و نباید کل استخراج شکست بخورد.
        private static string SafeNormalize(string value)
        {
            try
            {
                return value.Normalize(NormalizationForm.FormKC);
            }
            catch (ArgumentException)
            {
                return value;
            }
        }

        // نگهبانِ «این کاراکتر حذف شود» - از '\0' استفاده می‌کنیم چون در متن معنادار نمی‌آید.
        private const char Drop = '\0';

        private static char Map(char ch)
        {
            switch (ch)
            {
                // ی: هر سه شکل عربی به «ی» فارسی
                case '\u064A': // ي
                case '\u0649': // ى
                case '\u0626': // ئ
                    return '\u06CC';

                // ک: کافِ عربی به «ک» فارسی
                case '\u0643': // ك
                    return '\u06A9';

                // الف با همزه/مد - برای جستجو با الف ساده یکی گرفته می‌شود
                case '\u0622': // آ
                case '\u0623': // أ
                case '\u0625': // إ
                case '\u0671': // ٱ
                    return '\u0627';

                case '\u0629': // ة
                    return '\u0647'; // ه

                case '\u0624': // ؤ
                    return '\u0648'; // و

                // کشیده و نشانه‌های بالا/پایینِ الف - در متن تایپی نیستند، در PDF گاهی هستند.
                // با کد نویسه نوشته شده‌اند چون در ویرایشگر نامرئی یا چسبان‌اند.
                case '\u0640': // کشیده
                case '\u0653': // مد
                case '\u0654': // همزه بالا
                case '\u0655': // همزه پایین
                case '\u0670': // الف خنجری
                    return Drop;

                // نشانه‌های نامرئیِ جهت‌دهی و پیوند - هیچ‌وقت بخشی از عبارت جستجو نیستند.
                case '\u200B': // فاصله‌ی صفر
                case '\u200D': // اتصال‌دهنده
                case '\u200E': // نشانه‌ی چپ‌به‌راست
                case '\u200F': // نشانه‌ی راست‌به‌چپ
                case '\u202A':
                case '\u202B':
                case '\u202C':
                case '\u202D':
                case '\u202E':
                case '\uFEFF': // BOM
                    return Drop;

                // نیم‌فاصله به فاصله: کاربر «بهروزرسانی» و «به‌روزرسانی» را یکسان می‌جوید.
                case '\u200C':
                    return ' ';
            }

            // اعراب (فتحه، کسره، تشدید و...)
            if (ch >= '\u064B' && ch <= '\u0652')
                return Drop;

            // ارقام عربی-هندی و فارسی به لاتین - داده‌ی سمت سرور لاتین است.
            if (ch >= '\u0660' && ch <= '\u0669')
                return (char)('0' + (ch - '\u0660'));
            if (ch >= '\u06F0' && ch <= '\u06F9')
                return (char)('0' + (ch - '\u06F0'));

            // هر نوع فضای خالی (تب، خط جدید، فاصله‌ی بی‌شکست) یک فاصله‌ی ساده می‌شود.
            if (char.IsWhiteSpace(ch))
                return ' ';

            return ch;
        }
    }
}
