using Microsoft.Data.SqlClient;
using System;

namespace HSEQ.API.ServiceConfiguration
{
    /// <summary>
    /// ترجمه‌ی خطای دیتابیسِ HSEQ به جمله‌ای که بگوید روی سرور چه باید کرد.
    ///
    /// ورودِ کاربر بعد از پاسخِ مثبتِ سامانه‌ی مدیریت کاربران هنوز یک کار با دیتابیس
    /// دارد: خواندنِ نقش از جدول Admins. هر خرابیِ آن در مرورگر فقط «خطای داخلی سامانه»
    /// (۵۰۰) است، و چند علتِ کاملاً متفاوت - لاگین رد شد، دیتابیس نیست، ساختار قدیمی
    /// است، دسترسی نیست، سرور پیدا نمی‌شود - بدون این ترجمه یکسان دیده می‌شوند.
    ///
    /// رمزِ رشته‌ی اتصال هرگز در خروجی نمی‌آید؛ فقط سرور، دیتابیس و نام لاگین.
    /// </summary>
    public static class HseqDatabaseDiagnostics
    {
        /// <summary>«سرور …، دیتابیس …، لاگین …» - بدون رمز.</summary>
        public static string Target(string connectionString)
        {
            var (server, database, login) = Names(connectionString);
            return server == null
                ? "رشته‌ی اتصال خوانده نشد"
                : $"سرور «{server}»، دیتابیس «{database}»، لاگین «{login}»";
        }

        public static string Describe(Exception exception, string connectionString = null)
        {
            var sql = FindSqlException(exception);
            if (sql == null)
            {
                var innermost = Innermost(exception);
                return $"{innermost.GetType().Name}: {innermost.Message}";
            }

            var (server, database, login) = Names(connectionString);
            var where = server == null ? "دیتابیس HSEQ" : $"دیتابیس «{database}» روی «{server}»";
            var who = login ?? "?";

            switch (sql.Number)
            {
                case 18456:
                    return $"SQL Server ورودِ لاگین «{who}» را رد کرد (خطای 18456): یا نام کاربری یا رمز در " +
                           "ConnectionStrings:DefaultConnection درست نیست، یا روی این SQL Server ورود با رمز " +
                           "(SQL Server and Windows Authentication mode) فعال نیست.";

                case 4060:
                    return $"{where} باز نشد (خطای 4060): این دیتابیس روی آن سرور نیست، یا لاگین «{who}» به آن نگاشت ندارد.";

                case 207:
                case 208:
                    return $"ساختار {where} قدیمی است (خطای {sql.Number}: {sql.Message}). اسکریپت migrations.sql " +
                           "در پوشه‌ی Database بسته روی آن اجرا نشده است - پس از پشتیبان‌گیری اجرایش کنید.";

                case 229:
                case 230:
                    return $"لاگین «{who}» اجازه‌ی کار با {where} را ندارد (خطای {sql.Number}). " +
                           "نقش‌های db_datareader و db_datawriter را روی این دیتابیس به آن بدهید.";

                case 2:
                case 53:
                case 40:
                case -1:
                    return $"SQL Server «{server ?? "?"}» پیدا نشد یا پاسخ نداد (خطای {sql.Number}): سرویس SQL Server روشن نیست، " +
                           "نام نمونه لازم است (مثلاً localhost\\SQLEXPRESS)، یا TCP/IP در SQL Server Configuration Manager خاموش است.";

                case -2:
                    return $"مهلت اتصال به «{server ?? "?"}» تمام شد (خطای -2): سرور هست ولی به‌موقع جواب نداد.";

                default:
                    return $"خطای SQL شماره‌ی {sql.Number} روی {where}: {sql.Message}";
            }
        }

        private static (string Server, string Database, string Login) Names(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return (null, null, null);
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                var login = builder.IntegratedSecurity ? "حساب ویندوزیِ Application Pool" : builder.UserID;
                return (builder.DataSource, builder.InitialCatalog, login);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
            {
                return (null, null, null);
            }
        }

        private static SqlException FindSqlException(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sql) return sql;
            }
            return null;
        }

        private static Exception Innermost(Exception exception)
        {
            var current = exception;
            while (current.InnerException != null) current = current.InnerException;
            return current;
        }
    }
}
