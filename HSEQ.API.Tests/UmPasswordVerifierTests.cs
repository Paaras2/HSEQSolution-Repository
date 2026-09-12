using System.Text;
using HSEQ.Shared.Services.Services;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HSEQ.API.Tests;

/// <summary>
/// راستی‌آزمایی رمز در برابر قالب‌هایی که در ستون Password دیتابیس سامانه‌ی
/// مدیریت کاربران وجود دارند.
///
/// این حساس‌ترین کد این مخزن است: اشتباه در جهت سهل‌گیرانه یعنی پذیرفتن رمز غلط،
/// و آن خرابی هیچ نشانه‌ی بیرونی ندارد - نه خطایی، نه لاگی، نه شکایتی. تا وقتی
/// کسی سوءاستفاده نکند، «کار می‌کند» به نظر می‌رسد.
/// </summary>
public class UmPasswordVerifierTests
{
    private static UmPasswordVerifier Verifier() => new(NullLogger<UmPasswordVerifier>.Instance);

    private static string Argon2Hash(string password, string saltText = "sixteenbytesalt!")
    {
        var salt = Encoding.UTF8.GetBytes(saltText);
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = 65536,
            Iterations = 3,
            DegreeOfParallelism = 1,
        };
        var hash = argon2.GetBytes(32);
        string B64(byte[] b) => System.Convert.ToBase64String(b).TrimEnd('=');
        return $"$argon2id$v=19$m=65536,t=3,p=1${B64(salt)}${B64(hash)}";
    }

    // -----------------------------------------------------------------------
    // Argon2id - قالب جاری سامانه‌ی UM
    // -----------------------------------------------------------------------

    [Fact]
    public void Argon2_accepts_the_correct_password()
    {
        var stored = Argon2Hash("correct-horse-battery");

        Assert.Equal(PasswordVerificationResult.Succeeded,
            Verifier().Verify(stored, "correct-horse-battery"));
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("correct-horse-batter")]   // یک کاراکتر کمتر
    [InlineData("correct-horse-batteryy")] // یک کاراکتر بیشتر
    [InlineData("Correct-Horse-Battery")]  // بزرگی حروف
    [InlineData("")]
    public void Argon2_rejects_anything_else(string attempt)
    {
        var stored = Argon2Hash("correct-horse-battery");

        Assert.NotEqual(PasswordVerificationResult.Succeeded, Verifier().Verify(stored, attempt));
    }

    [Fact]
    public void Argon2_with_a_damaged_string_is_unsupported_not_accepted()
    {
        Assert.Equal(PasswordVerificationResult.UnsupportedFormat,
            Verifier().Verify("$argon2id$v=19$m=65536,t=3,p=1$only-four-parts", "anything"));
    }

    // -----------------------------------------------------------------------
    // رمز پیش‌فرض: کد ملی، متن ساده
    // -----------------------------------------------------------------------

    [Fact]
    public void A_plaintext_national_code_matches_itself()
    {
        Assert.Equal(PasswordVerificationResult.Succeeded, Verifier().Verify("1234567890", "1234567890"));
    }

    [Theory]
    [InlineData("1234567891")]
    [InlineData("123456789")]
    [InlineData("")]
    public void A_plaintext_national_code_rejects_anything_else(string attempt)
    {
        Assert.NotEqual(PasswordVerificationResult.Succeeded, Verifier().Verify("1234567890", attempt));
    }

    // -----------------------------------------------------------------------
    // قالب ناشناخته - مهم‌ترین بخش این کلاس
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // قالب قدیمی: PBKDF2-SHA1، ۱۰٬۰۰۰ تکرار، salt(20) ‖ key(20)
    // -----------------------------------------------------------------------

    /// <summary>همان کاری که Crypto.GenerateKeyHash می‌کند.</summary>
    private static string LegacyHash(string password)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20);
        var key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 10_000,
            System.Security.Cryptography.HashAlgorithmName.SHA1, 20);

        var combined = new byte[40];
        System.Buffer.BlockCopy(salt, 0, combined, 0, 20);
        System.Buffer.BlockCopy(key, 0, combined, 20, 20);
        return System.Convert.ToBase64String(combined);
    }

    [Fact]
    public void The_legacy_format_accepts_the_correct_password()
    {
        var stored = LegacyHash("my-old-password");
        Assert.Equal(56, stored.Length);

        Assert.Equal(PasswordVerificationResult.Succeeded,
            Verifier().Verify(stored, "my-old-password"));
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("my-old-passwor")]
    [InlineData("My-Old-Password")]
    [InlineData("")]
    public void The_legacy_format_rejects_anything_else(string attempt)
    {
        Assert.NotEqual(PasswordVerificationResult.Succeeded,
            Verifier().Verify(LegacyHash("my-old-password"), attempt));
    }

    /// <summary>
    /// هش ذخیره‌شده نباید خودش به‌عنوان رمز پذیرفته شود - وگرنه هر کسی که به
    /// دیتابیس بخواند می‌توانست وارد شود.
    /// </summary>
    [Fact]
    public void The_stored_legacy_hash_is_not_its_own_password()
    {
        var stored = LegacyHash("my-old-password");

        Assert.NotEqual(PasswordVerificationResult.Succeeded, Verifier().Verify(stored, stored));
    }

    /// <summary>
    /// طول ثابتِ ۴۰ بایت بخشی از تشخیص است: base64 معتبر با طول دیگر نباید به
    /// اشتباه به‌عنوان قالب قدیمی تفسیر شود.
    /// </summary>
    [Theory]
    [InlineData(32)]
    [InlineData(39)]
    [InlineData(41)]
    [InlineData(64)]
    public void Base64_of_another_length_is_unsupported_not_misread(int byteLength)
    {
        var stored = System.Convert.ToBase64String(new byte[byteLength]);

        Assert.Equal(PasswordVerificationResult.UnsupportedFormat, Verifier().Verify(stored, "anything"));
    }

    /// <summary>
    /// هیچ قالب ناشناخته‌ای نباید به‌عنوان متن ساده مقایسه شود. اگر شرطِ متن ساده
    /// گشاد بود، یک هشِ ذخیره‌شده با فرستادن خودِ همان هش به‌عنوان رمز پذیرفته
    /// می‌شد - یعنی هر کسی که به دیتابیس بخواند می‌توانست وارد شود.
    /// </summary>
    [Theory]
    [InlineData("aGVsbG8gd29ybGQgdGhpcyBpcyBub3QgcGxhaW4=")]
    [InlineData("5f4dcc3b5aa765d61d8327deb882cf99")]
    [InlineData("not-a-known-format")]
    [InlineData("12345678901234567890")]
    public void An_unknown_format_is_never_matched_against_itself(string stored)
    {
        Assert.NotEqual(PasswordVerificationResult.Succeeded, Verifier().Verify(stored, stored));
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("x", null)]
    [InlineData("", "")]
    public void Missing_values_fail_rather_than_throw(string stored, string provided)
    {
        Assert.Equal(PasswordVerificationResult.Failed, Verifier().Verify(stored, provided));
    }
}
