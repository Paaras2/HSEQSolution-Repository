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
}

app.Run();
