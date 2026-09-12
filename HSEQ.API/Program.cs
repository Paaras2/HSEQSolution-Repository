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
// ---------------------------------------------------------------------------
// از کجا اعتبار کاربر سنجیده شود
// ---------------------------------------------------------------------------
//   "Api"       سرویس HTTP سامانه‌ی مدیریت کاربران - پیش‌فرض، و همان چیزی که
//               منطق احراز هویت را در یک جا نگه می‌دارد.
//   "Database"  خواندن مستقیم از دیتابیس UM روی همان SQL Server، با همان
//               اعتبارنامه‌ی دیتابیس اصلی.
//
// حالت Database برای وقتی است که سرویس HTTP از سرور برنامه در دسترس نباشد.
// عمداً پیش‌فرض نیست: منطق راستی‌آزمایی رمز آن‌وقت در دو جا وجود دارد و هر قاعده‌ای
// که سامانه‌ی UM بعداً اضافه کند (قفل حساب، انقضای رمز، OTP) در این مسیر نیست.
var umSource = configuration.GetValue("UserManagement:Source", "Api");

builder.Services.AddScoped<UmPasswordVerifier>();

if (string.Equals(umSource, "Database", StringComparison.OrdinalIgnoreCase))
{
    var umConnectionString = UmConnectionString.Build(
        configuration["ConnectionStrings:DefaultConnection"],
        configuration.GetValue("UserManagement:Database", "UserManagement"),
        configuration["ConnectionStrings:UserManagement"]);

    builder.Services.AddScoped<IUMService>(provider => new UmDatabaseService(
        umConnectionString,
        provider.GetRequiredService<UmPasswordVerifier>(),
        provider.GetRequiredService<ILogger<UmDatabaseService>>()));
}
else
{
    builder.Services.AddScoped<IUMService, UmService>();
}

// مهلتِ تماس با سرویس مدیریت کاربران.
//
// بدون این، مقدار پیش‌فرض HttpClient اعمال می‌شد: ۱۰۰ ثانیه. وقتی فایروال بسته‌ها را
// بی‌صدا دور می‌ریزد (نه رد می‌کند)، کاربر یک دقیقه و نیم روی دکمه‌ی «ورود» منتظر
// می‌ماند و بعد پیام خطا می‌گیرد - و در آن فاصله معمولاً چند بار دیگر هم کلیک می‌کند.
//
// ۱۰ ثانیه برای یک فراخوانِ احراز هویت در شبکه‌ی داخلی زیاد هم هست. اگر سرویس UM
// واقعاً کند است، از همین کلید بالا ببرید.
var umTimeout = TimeSpan.FromSeconds(configuration.GetValue("UserManagementAPI:TimeoutSeconds", 10));

// فقط در حالت Api. در حالت Database این ثبت، IUMService را دوباره به UmService
// برمی‌گرداند (آخرین ثبت برنده است) و تنظیمات را بی‌سروصدا بی‌اثر می‌کند.
if (!string.Equals(umSource, "Database", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IUMService, UmService>(client => client.Timeout = umTimeout);
}

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

// پورت عمومی HTTPS برای هدایت (بخش Https:RedirectToHttps پایین‌تر). بدون این مقدار،
// میان‌افزار سعی می‌کند پورت را از بایندینگ‌های سرور حدس بزند - که پشت IIS و روی
// پورت غیراستاندارد جواب نمی‌دهد. عمداً اختیاری است: اگر ندهید یعنی ۴۴۳.
var httpsPort = configuration.GetValue<int?>("Https:Port");
if (httpsPort is > 0)
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsPort);
}

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
// مالکِ پیشوند /api
// ---------------------------------------------------------------------------
// قرارداد عمومی - همان چیزی که مرورگر صدا می‌زند - این است:
//
//     /api/Auth/login
//
// این پیشوند دقیقاً *یک* مالک دارد: میزبان. کنترلرها فقط نام خودشان را اعلام
// می‌کنند ([Route("[controller]")])، پس هیچ‌جای کد دوباره «api» نمی‌نویسد.
//
//   - زیر IIS: بک‌اند یک Application با مسیر /api است. خودِ IIS پیشوند را به‌عنوان
//     PathBase برمی‌دارد و /Auth/login را تحویل برنامه می‌دهد - یعنی همان چیزی که
//     کنترلر انتظار دارد. اینجا هیچ کاری لازم نیست.
//
//   - روی Kestrel (توسعه، اجرای مستقیم dotnet HSEQ.API.dll، آزمون دود): هیچ
//     میزبانی پیشوند را برنمی‌دارد، پس خودِ برنامه با UsePathBase همان کار را
//     می‌کند. نتیجه: /api/Auth/login در هر دو چیدمان به یک اکشن می‌رسد.
//
// شرطِ «PathBase خالی باشد» عمدی است و دو چیز را تضمین می‌کند:
//   ۱) وقتی میزبان پیشوند را اعلام کرده، برنامه دوباره برنمی‌دارد - پس مسیرِ
//      دوتایی /api/api/Auth/login جزو قرارداد عمومی نیست و ۴۰۴ می‌گیرد.
//   ۲) اگر روزی Application با مسیر دیگری (مثلاً /hseq-api) ثبت شود، همه‌چیز
//      بدون تغییر کد کار می‌کند، چون مالکِ پیشوند همچنان میزبان است.
//
// برخلاف نسخه‌ی قبلی، مسیرِ درخواست بازنویسی نمی‌شود و PathBase هم پاک نمی‌شود:
// PathBase دست‌نخورده می‌ماند، پس هر نشانی‌ای که برنامه تولید می‌کند (ریدایرکت،
// هدر Location، نشانی سرورِ Swagger) پیشوند /api را با خود دارد.
var apiPathBase = (configuration.GetValue("Api:PathBase", "/api") ?? string.Empty).Trim();

if (!string.IsNullOrEmpty(apiPathBase))
{
    // «api» به‌جای «/api» یک اشتباه تایپیِ محتمل در فایل تنظیمات است. بدون این
    // اصلاح، PathString همان‌جا استثنا می‌داد و برنامه اصلاً بالا نمی‌آمد - یعنی
    // یک اسلشِ جاافتاده کل سامانه را می‌خواباند. اسلشِ انتهایی هم برداشته می‌شود،
    // چون «/api/» با «/api» یکی نیست و مسیرها را به هم می‌ریزد.
    if (!apiPathBase.StartsWith('/')) apiPathBase = '/' + apiPathBase;
    apiPathBase = apiPathBase.TrimEnd('/');
}

// ---------------------------------------------------------------------------
// کلاینت و API روی یک سایت
// ---------------------------------------------------------------------------
// اگر فایل‌های build شده‌ی React در wwwroot باشند، همین برنامه آن‌ها را هم سرو
// می‌کند. یعنی در IIS فقط *یک* سایت لازم است و دیگر خبری از Application جداگانه
// زیر /api نیست.
//
// چرا این چیدمان بهتر است: پیشوند /api دیگر بین IIS و برنامه دست‌به‌دست نمی‌شود.
// همه‌ی خرابی‌هایی که از همان تقسیم می‌آمدند - ۴۰۴ روی /api/Auth/login، جواب دادنِ
// /api/api/...، فایل‌های کلاینت که یک لایه پایین‌تر می‌افتادند - در این حالت اصلاً
// موضوعیت ندارند. CORS هم لازم نیست، چون مبدأ یکی است.
//
// تشخیص خودکار است: بودنِ wwwroot/index.html یعنی بسته کلاینت را با خود دارد.
// همان باینری بدون آن فایل، دقیقاً مثل قبل فقط API است و می‌تواند زیر /api به‌عنوان
// Application بنشیند. هیچ سوئیچی لازم نیست و هر دو چیدمان از یک بسته درمی‌آیند.
var clientIndexPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
var servesClient = !string.IsNullOrEmpty(app.Environment.WebRootPath) && File.Exists(clientIndexPath);

if (servesClient)
{
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            // index.html هرگز کش نمی‌شود. نام فایل‌های دیگر هش دارد و با هر build
            // عوض می‌شود، ولی نامِ index.html ثابت است - اگر مرورگر نسخه‌ی کش‌شده را
            // نگه دارد، پس از استقرار همچنان به فایل‌های قدیمی ارجاع می‌دهد که دیگر
            // وجود ندارند. نتیجه‌اش صفحه‌ی سفید است، برای کاربری که هیچ کاری نکرده.
            var name = context.File.Name;
            context.Context.Response.Headers.CacheControl =
                name.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                    ? "no-cache, no-store, must-revalidate"
                    : "public, max-age=31536000, immutable";
        }
    });

    // بازگشتِ مسیرهای سمت کلاینت.
    //
    // مسیرهای React Router (/documents، /admin، /documents/42/revisions) روی دیسک
    // فایلی ندارند. بدون این، رفرش کردن یا باز کردن مستقیمِ لینک ۴۰۴ می‌گرفت.
    //
    // سه شرط - همان سه شرطی که در چیدمان دو-سایتی داخل web.config بودند:
    //
    //   ۱) زیر پیشوند API نباشد. مسیر ناموجودِ API باید ۴۰۴ بماند، نه اینکه صفحه‌ی
    //      HTML بگیرد؛ وگرنه کلاینتی که منتظر JSON است متن index.html را پارس
    //      می‌کند و خطایی می‌دهد که هیچ ربطی به علت واقعی ندارد.
    //   ۲) فقط GET. یک POST به مسیر اشتباه باید ۴۰۴ بگیرد، نه صفحه.
    //   ۳) پسوند نداشته باشد. «/assets/index-abc123.js» که پیدا نشده یعنی فایل
    //      واقعاً غایب است - آن هم باید ۴۰۴ بگیرد تا خرابی دیده شود، نه اینکه
    //      بی‌سروصدا HTML تحویل بدهد.
    //
    // جای این میان‌افزار عمدی است: *بعد* از فایل‌های استاتیک، پس فایل واقعی همیشه
    // برنده است؛ و *پیش* از UsePathBase، پس مسیر را دست‌نخورده و با پیشوند می‌بیند.
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var isApiPath = !string.IsNullOrEmpty(apiPathBase) && path.StartsWithSegments(apiPathBase);

        if (!isApiPath
            && HttpMethods.IsGet(context.Request.Method)
            && !Path.HasExtension(path.Value))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await context.Response.SendFileAsync(clientIndexPath);
            return;
        }

        await next(context);
    });
}

// در لاگ راه‌اندازی صریح گفته می‌شود کدام چیدمان فعال است. بدون این، تشخیص اینکه
// «چرا ریشه‌ی سایت ۴۰۴ می‌دهد» به حدس زدن ختم می‌شد.
app.Logger.LogInformation(
    servesClient
        ? "چیدمان تک‌سایتی: کلاینت از wwwroot سرو می‌شود و API زیر {PathBase}."
        : "چیدمان فقط-API: wwwroot/index.html نیست، پس کلاینت جای دیگری سرو می‌شود. API زیر {PathBase}.",
    string.IsNullOrEmpty(apiPathBase) ? "/" : apiPathBase);

if (!string.IsNullOrEmpty(apiPathBase))
{
    app.UseWhen(
        context => !context.Request.PathBase.HasValue,
        branch => branch.UsePathBase(apiPathBase));
}

// مسیریابی صریحاً همین‌جا شروع می‌شود - بعد از تعیین PathBase، نه قبلش.
//
// بدون این فراخوانی، ASP.NET Core خودش UseRouting را در *ابتدای* pipeline درج
// می‌کند؛ یعنی انتخاب endpoint پیش از میان‌افزار بالا انجام می‌شد و روی مسیرِ
// دست‌نخورده (/api/Auth/login به‌جای /Auth/login) نگاه می‌کرد و ۴۰۴ می‌داد.
//
// همین ترتیب بود که روی سرور شکست: /api/Auth/login خطای ۴۰۴ می‌داد ولی
// /api/api/Auth/login کار می‌کرد. با فراخوانی صریح، مسیریابی همین‌جا می‌نشیند.
app.UseRouting();

// Pipeline تنظیمات

// مستندات API فقط در محیط توسعه سرو می‌شود. قبلاً بدون شرط فعال بود و روی ریشه‌ی سایت
// می‌نشست؛ یعنی در محیط عملیاتی هم فهرست کامل اندپوینت‌ها برای همه قابل دیدن بود.
// مسیر هم از ریشه به "/swagger" منتقل شد تا ریشه برای خودِ برنامه آزاد بماند.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // نشانی نسبی، نه مطلق. با نشانی مطلق («/swagger/...») مرورگر آن را از ریشه‌ی
        // مبدأ حساب می‌کرد و زیر PathBase=/api به /swagger/v1/swagger.json می‌رفت که
        // وجود ندارد. نسبی بودن یعنی از دلِ صفحه‌ی UI حساب می‌شود، پس در هر دو
        // چیدمان به {PathBase}/swagger/v1/swagger.json می‌رسد.
        options.SwaggerEndpoint("v1/swagger.json", "HSEQ");
        options.RoutePrefix = "swagger";
    });
}

// هدایت به HTTPS یک تصمیم صریح است، نه نتیجه‌ی جانبیِ نام محیط.
//
// پیش از این هر محیطی غیر از Development این میان‌افزار را روشن می‌کرد. سایت روی
// یک بایندینگ HTTP سرو می‌شود، پس میان‌افزار پورت HTTPS را پیدا نمی‌کرد، هیچ
// هدایتی هم انجام نمی‌داد و فقط *به‌ازای هر درخواست* یک هشدار در لاگ می‌نوشت:
//
//     Failed to determine the https port for redirect.
//
// دو ایراد داشت: لاگ را پر می‌کرد تا جایی که هشدارهای واقعی گم می‌شدند، و اگر
// روزی یک بایندینگ HTTPS به سایت اضافه می‌شد، رفتار برنامه بی‌آنکه کسی چیزی
// عوض کرده باشد به «هدایت اجباری» تغییر می‌کرد.
//
// حالا با تنظیمات کنترل می‌شود:
//   Https:RedirectToHttps  - پیش‌فرض false، یعنی همان رفتار مؤثرِ فعلی
//   Https:Port             - فقط وقتی پورت عمومی HTTPS چیزی جز ۴۴۳ است
if (configuration.GetValue("Https:RedirectToHttps", false))
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
        // خطای دیتابیس اینجا نباید برنامه را بکشد.
        //
        // پیش از این، یک خطای اتصال در seed یک استثنای مدیریت‌نشده در مسیر راه‌اندازی
        // بود و کل برنامه بالا نمی‌آمد. نتیجه‌اش برای کسی که استقرار می‌دهد: IIS خطای
        // ۵۰۰ خام می‌داد، /api/Health هم جواب نمی‌داد چون برنامه‌ای وجود نداشت، و هیچ
        // چیزی نمی‌گفت مشکل از دیتابیس است - مگر آنکه لاگ stdout از قبل روشن بوده باشد،
        // که پیش‌فرض نیست. یعنی دقیقاً همان تنها راهنمایی که لازم بود، در دسترس نبود.
        //
        // با مهار خطا، برنامه بالا می‌آید و /api/Health پاسخ Unhealthy می‌دهد - یعنی
        // همان تشخیصی که برای رفع مشکل لازم است، بدون هیچ پیکربندی اضافه.
        // seed خودش upsert است، پس اجرای دوباره‌اش پس از رفع مشکل بی‌خطر است.
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
                                        .CreateLogger("Startup.Seed");
        try
        {
            var seedService = scope.ServiceProvider.GetRequiredService<ISeedDatabase>();
            await seedService.Seed();
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex,
                "نوشتن داده‌ی پایه هنگام راه‌اندازی شکست خورد. برنامه بالا می‌آید ولی " +
                "تا وقتی دیتابیس در دسترس نباشد ناسالم است - وضعیتش را از /api/Health ببینید.");
        }
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
