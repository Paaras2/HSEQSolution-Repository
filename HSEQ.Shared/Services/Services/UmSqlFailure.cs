using Microsoft.Data.SqlClient;

namespace HSEQ.Shared.Services.Services
{
    /// <summary>
    /// ترجمه‌ی خطای SQL به جمله‌ای که بگوید باید چه کار کرد.
    ///
    /// چهار خرابیِ کاملاً متفاوت، همگی از راه SqlException می‌آیند و اگر همه را
    /// «اتصال به دیتابیس ممکن نشد» بنویسیم، کسی که لاگ را می‌خواند نمی‌فهمد مشکل
    /// شبکه است یا دسترسی یا نام دیتابیس - و همان ابهام است که عیب‌یابی را به
    /// حدس‌زدن تبدیل می‌کند.
    ///
    /// محتمل‌ترینشان در این استقرار ۴۰۶۰ است: لاگینِ SQL روی دیتابیس اصلی مجاز است
    /// ولی روی دیتابیس سامانه‌ی مدیریت کاربران نگاشت ندارد. تا لحظه‌ی اولین ورودِ
    /// کاربر هیچ نشانه‌ای ندارد.
    /// </summary>
    public static class UmSqlFailure
    {
        public static string Describe(SqlException ex, string database, string server)
        {
            switch (ex.Number)
            {
                case 4060:
                    return $"دیتابیس «{database}» روی «{server}» باز نشد: این لاگین به آن دیتابیس دسترسی ندارد. " +
                           $"روی SQL Server اجرا کنید: USE [{database}]; CREATE USER [<لاگین>] FOR LOGIN [<لاگین>]; " +
                           $"ALTER ROLE db_datareader ADD MEMBER [<لاگین>];";

                case 18456:
                    return $"ورود به «{server}» رد شد: نام کاربری یا رمز SQL در ConnectionStrings درست نیست.";

                case 208:
                case 229:
                case 230:
                    return $"جدول dbo.Users در «{database}» خوانده نشد: یا وجود ندارد یا این لاگین اجازه‌ی SELECT روی آن را ندارد.";

                case 2:
                case 53:
                case 40:
                case -1:
                    return $"سرور «{server}» پاسخ نداد: نام میزبان از این ماشین پیدا نشد یا پورت ۱۴۳۳ بسته است.";

                case -2:
                    return $"مهلت اتصال به «{server}» تمام شد: سرور در دسترس هست ولی به‌موقع جواب نداد.";

                default:
                    return $"خطای SQL شماره‌ی {ex.Number} هنگام کار با دیتابیس «{database}» روی «{server}».";
            }
        }
    }
}
