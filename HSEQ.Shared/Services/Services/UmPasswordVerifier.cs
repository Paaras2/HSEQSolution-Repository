using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Text;

namespace HSEQ.Shared.Services.Services
{
    /// <summary>
    /// نتیجه‌ی راستی‌آزمایی یک رمز در برابر مقدار ذخیره‌شده.
    ///
    /// سه حالت است، نه دو تا. «قالب را نمی‌شناسم» با «رمز غلط است» یکی نیست: اولی
    /// یعنی خودِ ما ناقصیم و باید در لاگ دیده شود، دومی یعنی کاربر اشتباه کرده.
    /// یکی کردنشان همان جایی است که یک ایراد پیاده‌سازی به‌شکل «رمز اشتباه» پنهان
    /// می‌ماند و هیچ‌کس نمی‌فهمد چرا ورودِ عده‌ای کار نمی‌کند.
    /// </summary>
    public enum PasswordVerificationResult
    {
        Failed,
        Succeeded,
        UnsupportedFormat,
    }

    /// <summary>
    /// راستی‌آزمایی رمز در برابر آنچه سرویس مدیریت کاربران در ستون Password
    /// می‌نویسد.
    ///
    /// در آن ستون بیش از یک قالب وجود دارد، چون سامانه‌ی UM در حال مهاجرت است:
    ///
    ///   $argon2id$v=19$...   قالب جاری. پارامترها و salt داخل خودِ رشته‌اند، پس
    ///                        بدون هیچ فرضی قابل راستی‌آزمایی است.
    ///
    ///   کد ملی، متن ساده    رمز پیش‌فرضِ کاربری که هنوز واردنشده (IsFirstLogin).
    ///                        ذخیره‌اش به این شکل ایراد امنیتی سامانه‌ی UM است، ولی
    ///                        واقعیتِ داده است و نادیده گرفتنش یعنی آن کاربران
    ///                        نتوانند وارد شوند.
    ///
    ///   base64 ۴۰ بایتی      قالب قدیمی: PBKDF2 با ۱۰٬۰۰۰ تکرار و SHA-1، که
    ///                        salt ۲۰ بایتی و کلید ۲۰ بایتی را پشت سر هم ذخیره
    ///                        می‌کند. الگوریتم در خودِ مقدار نوشته نشده، پس از
    ///                        Crypto.GenerateKeyHash در پروژه‌ی همسایه (Mission)
    ///                        برداشته شد - همان کلاس کمکی که بین پروژه‌ها کپی شده.
    ///                        چیدمانش با داده جور است: ۲۰+۲۰ بایت، و ۲۰ بایت
    ///                        دقیقاً اندازه‌ی خروجی SHA-1 است.
    ///
    /// این مسیر فقط *راستی‌آزمایی* می‌کند و هرگز رمز تازه‌ای نمی‌سازد. PBKDF2-SHA1
    /// با ۱۰٬۰۰۰ تکرار امروز ضعیف است، ولی تصمیمِ سامانه‌ی UM است و تغییرش از اینجا
    /// یعنی قفل شدن همان کاربران.
    /// </summary>
    public class UmPasswordVerifier
    {
        private readonly ILogger<UmPasswordVerifier> _logger;

        public UmPasswordVerifier(ILogger<UmPasswordVerifier> logger) => _logger = logger;

        public PasswordVerificationResult Verify(string stored, string provided)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(provided))
                return PasswordVerificationResult.Failed;

            if (stored.StartsWith("$argon2", StringComparison.Ordinal))
                return VerifyArgon2(stored, provided);

            // رمز پیش‌فرض: کد ملی، بدون درهم‌سازی. مقایسه در زمان ثابت انجام می‌شود
            // تا طول و محتوای مقدار ذخیره‌شده از راه زمانِ پاسخ لو نرود.
            if (LooksLikePlaintextDefault(stored))
            {
                return FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(provided))
                    ? PasswordVerificationResult.Succeeded
                    : PasswordVerificationResult.Failed;
            }

            if (TryReadLegacyPair(stored, out var salt, out var expectedKey))
                return VerifyLegacyPbkdf2(salt, expectedKey, provided);

            return PasswordVerificationResult.UnsupportedFormat;
        }

        // قالب قدیمی: دقیقاً ۴۰ بایت، نیمه‌ی اول salt و نیمه‌ی دوم کلید.
        // طول ثابت خودش بخشی از تشخیص است - هر base64 دیگری با طول متفاوت رد
        // می‌شود و به UnsupportedFormat می‌رسد، نه اینکه اشتباه تفسیر شود.
        private const int LegacySaltLength = 20;
        private const int LegacyKeyLength = 20;
        private const int LegacyIterations = 10_000;

        private static bool TryReadLegacyPair(string stored, out byte[] salt, out byte[] key)
        {
            salt = null;
            key = null;

            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(stored);
            }
            catch (FormatException)
            {
                return false;
            }

            if (raw.Length != LegacySaltLength + LegacyKeyLength) return false;

            salt = new byte[LegacySaltLength];
            key = new byte[LegacyKeyLength];
            Buffer.BlockCopy(raw, 0, salt, 0, LegacySaltLength);
            Buffer.BlockCopy(raw, LegacySaltLength, key, 0, LegacyKeyLength);
            return true;
        }

        private static PasswordVerificationResult VerifyLegacyPbkdf2(byte[] salt, byte[] expectedKey, string provided)
        {
            // SHA-1 صریح نوشته شده، نه به‌صورت پیش‌فرض: سازنده‌ی قدیمیِ
            // Rfc2898DeriveBytes بدون ذکر الگوریتم SHA-1 می‌گیرد، و همان رفتار است
            // که این هش‌ها با آن ساخته شده‌اند. نوشتنش صریح یعنی اگر روزی پیش‌فرضِ
            // پلتفرم عوض شود، ورودِ این کاربران بی‌سروصدا نمی‌شکند.
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(provided),
                salt,
                LegacyIterations,
                HashAlgorithmName.SHA1,
                LegacyKeyLength);

            return FixedTimeEquals(expectedKey, actual)
                ? PasswordVerificationResult.Succeeded
                : PasswordVerificationResult.Failed;
        }

        /// <summary>
        /// رمز پیش‌فرضِ سامانه‌ی UM کد ملی است: رشته‌ای کوتاه و تماماً رقمی. این شرط
        /// عمداً تنگ است - هر چیزی که شبیهش نباشد به‌عنوان «قالب ناشناخته» رد می‌شود،
        /// نه اینکه به‌عنوان متن ساده مقایسه شود.
        /// </summary>
        private static bool LooksLikePlaintextDefault(string stored)
        {
            if (stored.Length is < 8 or > 11) return false;
            foreach (var c in stored)
            {
                if (c is < '0' or > '9') return false;
            }
            return true;
        }

        private PasswordVerificationResult VerifyArgon2(string stored, string provided)
        {
            try
            {
                // قالب PHC:  $argon2id$v=19$m=65536,t=3,p=1$<salt>$<hash>
                var parts = stored.Split('$');
                if (parts.Length != 6) return PasswordVerificationResult.UnsupportedFormat;

                var options = parts[3].Split(',');
                var memory = ParseOption(options, "m=");
                var iterations = ParseOption(options, "t=");
                var parallelism = ParseOption(options, "p=");
                if (memory <= 0 || iterations <= 0 || parallelism <= 0)
                    return PasswordVerificationResult.UnsupportedFormat;

                var salt = FromBase64NoPadding(parts[4]);
                var expected = FromBase64NoPadding(parts[5]);

                using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(provided))
                {
                    Salt = salt,
                    MemorySize = memory,
                    Iterations = iterations,
                    DegreeOfParallelism = parallelism,
                };

                var actual = argon2.GetBytes(expected.Length);
                return FixedTimeEquals(expected, actual)
                    ? PasswordVerificationResult.Succeeded
                    : PasswordVerificationResult.Failed;
            }
            catch (Exception ex)
            {
                // رشته‌ی ذخیره‌شده هرگز لاگ نمی‌شود - خودش ماده‌ی اعتبارنامه است.
                _logger.LogError(ex, "راستی‌آزمایی Argon2 شکست خورد: قالب مقدار ذخیره‌شده خوانده نشد.");
                return PasswordVerificationResult.UnsupportedFormat;
            }
        }

        private static int ParseOption(string[] options, string prefix)
        {
            foreach (var option in options)
            {
                var trimmed = option.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(trimmed.Substring(prefix.Length), out var value))
                {
                    return value;
                }
            }
            return -1;
        }

        // قالب PHC بدون '=' پایانی می‌نویسد؛ Convert.FromBase64String آن را نمی‌پذیرد.
        private static byte[] FromBase64NoPadding(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        // مقایسه در زمان ثابت: خروجِ زودهنگام در اولین بایتِ متفاوت، طول پیشوندِ
        // درست را از راه زمانِ پاسخ قابل اندازه‌گیری می‌کند.
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
