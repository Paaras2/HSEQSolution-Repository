using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace HSEQ.Shared.Services.Services
{
    /// <summary>
    /// وارسی دیتابیس سامانه‌ی مدیریت کاربران، یک بار هنگام بالا آمدن برنامه.
    ///
    /// چرایی‌اش: در حالت Database، هر چیزی که اشتباه باشد - دسترسی نداشتن لاگین به
    /// این دیتابیس، نبودن جدول، ستونی که نامش عوض شده - تا لحظه‌ی اولین ورودِ یک
    /// کاربر واقعی هیچ نشانه‌ای ندارد، و آن وقت هم فقط به شکل «۵۰۳» دیده می‌شود.
    /// این وارسی همان خرابی را به بالا آمدن برنامه می‌آورد، جایی که یک بار دیده
    /// می‌شود و لاگش صریح است.
    ///
    /// عمداً برنامه را متوقف نمی‌کند: خرابیِ دیتابیس UM نباید جلوی سرو شدن خودِ
    /// سامانه را بگیرد؛ فقط ورود کار نمی‌کند، و آن هم باید در لاگ پیدا باشد.
    ///
    /// هیچ رمزی - نه ساده، نه درهم‌شده - خوانده یا نوشته نمی‌شود. فقط شمارش.
    /// </summary>
    public static class UmDatabaseProbe
    {
        private static readonly string[] RequiredColumns =
        {
            "PCode", "FirstName", "LastName", "Mobile", "IsActive",
            "IsFirstLogin", "NationalCode", "Username", "LastModificationDate", "Password",
        };

        // همان ترتیب و همان شرط‌هایی که UmPasswordVerifier به کار می‌برد، وگرنه این
        // شمارش چیزی را می‌شمارد که آن کد نمی‌بیند.
        private const string ShapeSql = @"
SELECT Shape, COUNT(*) AS Users FROM (
    SELECT CASE
        WHEN Password LIKE '$argon2%' THEN 'argon2'
        WHEN LEN(Password) BETWEEN 8 AND 11 AND Password NOT LIKE '%[^0-9]%' THEN 'plaintext'
        WHEN LEN(Password) = 56 AND Password NOT LIKE '%[^A-Za-z0-9+/=]%' THEN 'legacy'
        ELSE 'unknown'
    END AS Shape
    FROM dbo.Users
) s GROUP BY Shape";

        public static async Task RunAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
        {
            string database = "?", server = "?";
            try
            {
                var parsed = new SqlConnectionStringBuilder(connectionString);
                database = parsed.InitialCatalog;
                server = parsed.DataSource;
            }
            catch (ArgumentException)
            {
                logger.LogCritical("رشته‌ی اتصال دیتابیس سامانه‌ی مدیریت کاربران خوانده نشد؛ ورود کاربران کار نخواهد کرد.");
                return;
            }

            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                var missing = await FindMissingColumnsAsync(connection, cancellationToken);
                if (missing.Count > 0)
                {
                    logger.LogCritical(
                        "جدول dbo.Users در «{Database}» این ستون‌ها را ندارد: {Missing}. ورود کاربران کار نخواهد کرد.",
                        database, string.Join("، ", missing));
                    return;
                }

                var shapes = await CountShapesAsync(connection, cancellationToken);
                var total = 0;
                foreach (var count in shapes.Values) total += count;

                logger.LogInformation(
                    "دیتابیس سامانه‌ی مدیریت کاربران در دسترس است: «{Database}» روی «{Server}»، {Total} کاربر " +
                    "({Argon2} آرگون۲، {Legacy} قالب قدیمی، {Plaintext} متن ساده).",
                    database, server, total,
                    Get(shapes, "argon2"), Get(shapes, "legacy"), Get(shapes, "plaintext"));

                // تنها عددی که واقعاً باید دید: کسانی که هیچ‌کدام از سه مسیر
                // راستی‌آزمایی نمی‌شناسدشان و هرگز نمی‌توانند وارد شوند.
                var unknown = Get(shapes, "unknown");
                if (unknown > 0)
                {
                    logger.LogCritical(
                        "رمز {Unknown} کاربر با قالبی ذخیره شده که این مسیر نمی‌شناسد؛ آن‌ها نمی‌توانند وارد شوند. " +
                        "برای این کاربران باید به حالت «Source: Api» برگشت.",
                        unknown);
                }
            }
            catch (SqlException ex)
            {
                logger.LogCritical(ex, "{Reason} تا وقتی این درست نشود هیچ‌کس نمی‌تواند وارد شود.",
                    UmSqlFailure.Describe(ex, database, server));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogCritical(ex, "وارسی دیتابیس سامانه‌ی مدیریت کاربران به نتیجه نرسید.");
            }
        }

        private static int Get(IReadOnlyDictionary<string, int> shapes, string key) =>
            shapes.TryGetValue(key, out var value) ? value : 0;

        private static async Task<List<string>> FindMissingColumnsAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using (var command = new SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='Users'",
                connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken)) present.Add(reader.GetString(0));
            }

            var missing = new List<string>();
            foreach (var column in RequiredColumns)
            {
                if (!present.Contains(column)) missing.Add(column);
            }
            return missing;
        }

        private static async Task<Dictionary<string, int>> CountShapesAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            var shapes = new Dictionary<string, int>(StringComparer.Ordinal);

            await using var command = new SqlCommand(ShapeSql, connection) { CommandTimeout = 30 };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                shapes[reader.GetString(0)] = reader.GetInt32(1);
            }
            return shapes;
        }
    }
}
