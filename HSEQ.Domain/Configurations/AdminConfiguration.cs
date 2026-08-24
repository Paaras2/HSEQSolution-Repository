using HSEQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;



//Fluent API
namespace HSEQ.Domain.Configurations
{
    public class AdminConfigurations : IEntityTypeConfiguration<Admin>
    {
        public void Configure(EntityTypeBuilder<Admin> builder)
        {
            builder.ToTable("Admins");
            builder.HasKey(a => a.Key);

            // هر کد پرسنلی فقط یک ردیف نقش دارد - وگرنه معلوم نبود کدام‌یک ملاک است.
            builder.HasIndex(a => a.Pcode).IsUnique();

            // عمداً HasDefaultValue ندارد. با پیش‌فرضِ دیتابیسی، EF هر جا مقدار برابر
            // CLR default (یعنی ۰) بود آن پیش‌فرض را می‌نوشت - و چون پیش‌فرض «مدیر سیستم»
            // بود، ارسال Role=0 از سمت کلاینت به ساخت یک مدیر سیستم ختم می‌شد.
            // مقداردهی ردیف‌های موجود در خودِ مهاجرت انجام می‌شود.

  
        }
    }
}

