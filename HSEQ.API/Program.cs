using HSEQ.API.ServiceConfiguration;
using HSEQ.Common;
using HSEQ.Domain;
using HSEQ.Service;
using HSEQ.Service.Interfaces.Services;
using HSEQ.Service.Services.Services;
using HSEQ.Shared.Interfaces.Services;
using HSEQ.Shared.Middlewares;
using HSEQ.Shared.Services.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// تنظیمات اولیه
var configuration = builder.Configuration;

// پیش از هر چیز: اگر تنظیمات ناقص است، همین‌جا با پیام صریح متوقف شو - نه وسط اولین
// درخواست کاربر و نه با مقادیر پیش‌فرضِ توسعه.
ProductionConfigurationValidator.Validate(configuration, builder.Environment);

AppSettingFactory.Initialize(configuration);
var appSettings = AppSettingFactory.AppSetting;

// ثبت سرویس‌ها
builder.Services.AddControllers();
builder.Services.AddApplicationLayerServices()
                .AddServiceLayerServices()
                .AddDomainLayerServices(configuration);
builder.Services.AddScoped<IUMService, UmService>();
builder.Services.AddHttpClient<IUMService, UmService>();

// تنظیمات Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = appSettings.JwtSettings.Issuer,
        ValidAudience = appSettings.JwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.JwtSettings.Key))
    };
});

builder.Services.AddSwaggerGen();

builder.Services.AddAuthorization();

// CORS: در محیط عملیاتی فقط دامنه‌های اعلام‌شده در appsettings ("Cors:AllowedOrigins")
// اجازه دارند. قبلاً AllowAnyOrigin بود، یعنی هر سایتی در شبکه می‌توانست از طرف مرورگرِ
// کاربرِ لاگین‌کرده به این API درخواست بزند.
//
// در Development عمداً باز می‌ماند تا پورت متغیرِ Vite کار توسعه را قفل نکند.
var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Cors", policy =>
    {
        if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// سازگاری با میزبانی زیر مسیر /api در IIS
// ---------------------------------------------------------------------------
// کنترلرها با [Route("api/[controller]")] نوشته شده‌اند، ولی روی سرور، بک‌اند
// به‌عنوان یک Application زیر مسیر /api ثبت می‌شود. در آن حالت IIS پیشوند /api را
// به‌عنوان PathBase برمی‌دارد و از مسیر حذف می‌کند، پس درخواستِ /api/Search به
// مسیرِ /Search می‌رسد و با هیچ کنترلری جور در نمی‌آید - یعنی همه‌ی فراخوان‌های
// کلاینت ۴۰۴ می‌گرفتند.
//
// این میان‌افزار همان پیشوند را به مسیر برمی‌گرداند تا مسیریابی در هر دو چیدمان
// یکسان رفتار کند:
//   - زیر Application با مسیر /api  →  PathBase="/api" و Path="/Search"  →  Path="/api/Search"
//   - در ریشه‌ی سایت یا روی Kestrel →  PathBase خالی است  →  هیچ تغییری نمی‌کند
//
// عمداً به‌جای عوض کردن مسیر کنترلرها یا آدرس کلاینت انتخاب شد: هیچ‌کدام از آن دو
// در محیط توسعه قابل آزمایش نبودند، ولی این یکی هست.
app.Use((context, next) =>
{
    if (context.Request.PathBase.HasValue)
    {
        context.Request.Path = context.Request.PathBase.Add(context.Request.Path);
        context.Request.PathBase = PathString.Empty;
    }

    return next(context);
});

// Pipeline تنظیمات

// مستندات API فقط در محیط توسعه سرو می‌شود. قبلاً بدون شرط فعال بود و روی ریشه‌ی سایت
// می‌نشست؛ یعنی در محیط عملیاتی هم فهرست کامل اندپوینت‌ها برای همه قابل دیدن بود.
// مسیر هم از ریشه به "/swagger" منتقل شد تا ریشه برای خودِ برنامه آزاد بماند.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HSEQ");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("Cors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// اعمال مهاجرت هنگام بالا آمدن، در توسعه راحت است ولی روی سرور عملیاتی یعنی هر
// ری‌استارتِ IIS می‌تواند ساختار دیتابیس را عوض کند - بدون پشتیبان، بدون بازبینی و
// بدون اینکه کسی خبر داشته باشد. پس بیرون از توسعه پیش‌فرض خاموش است و اسکریپت
// استقرار، مهاجرت را به‌صورت یک گام کنترل‌شده و پس از پشتیبان‌گیری اجرا می‌کند.
var migrateOnStartup = configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment());
var seedOnStartup = configuration.GetValue("Database:SeedOnStartup", true);

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
    if (context is null)
        throw new Exception("Database Context Not Found");

    if (migrateOnStartup)
        await context.Database.MigrateAsync();

    // Seed فقط داده‌ی پایه‌ی دامنه را می‌نویسد (مدیریت‌ها، فعالیت‌ها، انواع سند و آرشیو
    // شماره‌های قدیمی) و همه‌جا upsert است، پس اجرای دوباره‌اش بی‌خطر است.
    if (seedOnStartup)
    {
        var seedService = scope.ServiceProvider.GetRequiredService<ISeedDatabase>();
        await seedService.Seed();
    }

    // ورود اسناد سامانه‌ی قدیمی - فقط وقتی صریحاً از خط فرمان خواسته شود:
    //   dotnet run -- --import-legacy <manifest.json> <پوشه‌ی فایل‌ها> [--dry-run]
    // عمداً هیچ مسیر HTTP ندارد: کاری یک‌باره و سنگین است، نه عملیاتی که از رابط
    // کاربری اجرا شود. پس از اتمام، برنامه بدون بالا آمدن وب‌سرور خارج می‌شود.
    var importIndex = Array.IndexOf(args, "--import-legacy");
    if (importIndex >= 0)
    {
        if (args.Length < importIndex + 3)
        {
            Console.Error.WriteLine("استفاده: --import-legacy <manifest.json> <پوشه‌ی فایل‌ها> [--dry-run]");
            return 1;
        }

        var manifestPath = args[importIndex + 1];
        var sourceDirectory = args[importIndex + 2];
        var dryRun = args.Contains("--dry-run");

        var importer = scope.ServiceProvider.GetRequiredService<ILegacyDocumentImportService>();
        var importResult = await importer.ImportAsync(manifestPath, sourceDirectory, dryRun);

        Console.WriteLine();
        Console.WriteLine(dryRun ? "=== ورود آزمایشی (بدون تغییر) ===" : "=== ورود اسناد قدیمی ===");
        Console.WriteLine($"  کل مانیفست     : {importResult.Total}");
        Console.WriteLine($"  درج‌شده        : {importResult.Inserted}");
        Console.WriteLine($"  از قبل موجود   : {importResult.AlreadyExisted}");
        Console.WriteLine($"  ناموفق         : {importResult.Failed}");
        foreach (var problem in importResult.Problems.Take(25))
            Console.WriteLine($"     - {problem}");
        if (importResult.Problems.Count > 25)
            Console.WriteLine($"     ... و {importResult.Problems.Count - 25} مورد دیگر");

        return importResult.Failed == 0 ? 0 : 2;
    }

    // بازسازی متن فایل اسناد برای «جستجو در محتوای فایل»:
    //   dotnet run -- --reindex-text [--only-missing]
    // اسنادی که پیش از استخراج‌کننده‌ی فعلی وارد شده‌اند متن قابل‌جستجو ندارند؛ این
    // گام آن‌ها را از روی فایل‌های روی دیسک از نو می‌سازد. همین کار از پنل ادمین هم
    // در دسترس است، این مسیر برای استقرار و اجرای دسته‌ای است.
    if (args.Contains("--reindex-text"))
    {
        var onlyMissing = args.Contains("--only-missing");

        var indexer = scope.ServiceProvider.GetRequiredService<IDocumentTextIndexService>();
        var indexResult = await indexer.ReindexAsync(onlyMissing);

        Console.WriteLine();
        Console.WriteLine(onlyMissing ? "=== نمایه‌سازی اسناد بدون متن ===" : "=== نمایه‌سازی کامل متن فایل‌ها ===");
        Console.WriteLine($"  بررسی‌شده        : {indexResult.Total}");
        Console.WriteLine($"  نمایه‌شده        : {indexResult.Indexed}");
        Console.WriteLine($"  بدون متن (اسکن)  : {indexResult.WithoutText}");
        Console.WriteLine($"  فایل ناموجود     : {indexResult.FileMissing}");
        Console.WriteLine($"  ناموفق           : {indexResult.Failed}");
        foreach (var problem in indexResult.Problems.Take(25))
            Console.WriteLine($"     - {problem}");

        return indexResult.Failed == 0 ? 0 : 2;
    }
}

app.Run();
return 0;
