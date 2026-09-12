using Microsoft.Data.SqlClient;
using System;

namespace HSEQ.Shared.Services.Services
{
    /// <summary>
    /// رشته‌ی اتصال به دیتابیس سامانه‌ی مدیریت کاربران.
    ///
    /// عمداً از رشته‌ی اتصال اصلی ساخته می‌شود و فقط نام دیتابیس عوض می‌شود. هر دو
    /// روی یک SQL Server و با یک اعتبارنامه‌اند، پس نوشتن رشته‌ی دوم یعنی همان رمز
    /// در دو جای فایل تنظیمات - و روزی که عوض شود، یکی‌شان جا می‌ماند و خرابی‌اش
    /// فقط در لحظه‌ی ورودِ کاربر پیدا می‌شود.
    ///
    /// اگر دیتابیس UM جای دیگری یا با اعتبارنامه‌ی دیگری باشد،
    /// ‎ConnectionStrings:UserManagement‎ صریحاً همه‌ی این‌ها را کنار می‌زند.
    /// </summary>
    public static class UmConnectionString
    {
        public static string Build(string defaultConnection, string databaseName, string explicitConnection)
        {
            if (!string.IsNullOrWhiteSpace(explicitConnection))
                return explicitConnection;

            if (string.IsNullOrWhiteSpace(defaultConnection))
                throw new InvalidOperationException(
                    "برای اتصال به دیتابیس سامانه‌ی مدیریت کاربران، 'ConnectionStrings:DefaultConnection' لازم است.");

            // با SqlConnectionStringBuilder، نه جایگزینی متنی: نام دیتابیس ممکن است
            // با هر کدام از کلیدهای Database / Initial Catalog نوشته شده باشد، و
            // مقدارها می‌توانند کوتیشن و فاصله داشته باشند.
            var builder = new SqlConnectionStringBuilder(defaultConnection)
            {
                InitialCatalog = string.IsNullOrWhiteSpace(databaseName) ? "UserManagement" : databaseName
            };

            return builder.ConnectionString;
        }
    }
}
