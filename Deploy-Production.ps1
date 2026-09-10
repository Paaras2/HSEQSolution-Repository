<#
.SYNOPSIS
    استقرار بسته‌ی انتشار HSEQ روی سرور ویندوزی با IIS.

.DESCRIPTION
    این اسکریپت روی سرور اجرا می‌شود، نه روی ماشین توسعه. ورودی‌اش پوشه‌ای است که
    Publish-Production.ps1 ساخته.

    ترتیب کار عمداً «همه‌ی بررسی‌ها اول، تغییرات بعد» است: تا وقتی تمام پیش‌نیازها
    تأیید نشده‌اند هیچ فایلی جابه‌جا نمی‌شود، تا استقرارِ نیمه‌کاره پیش نیاید.

    با -DryRun هیچ تغییری اعمال نمی‌شود و فقط گزارش می‌دهد چه کارهایی انجام می‌شد.

.PARAMETER PackagePath
    مسیر پوشه‌ی بسته (همان پوشه‌ای که release.json داخلش است).

.PARAMETER SiteName
    نام سایت IIS. اگر نباشد ساخته می‌شود.

.PARAMETER SitePath
    مسیر فیزیکی فایل‌های کلاینت.

.PARAMETER ApiPath
    مسیر فیزیکی بک‌اند. به‌صورت Application با مسیر /api زیر همان سایت ثبت می‌شود.

.PARAMETER DataPath
    مسیر داده‌های ماندگار: فایل مدارک و لاگ‌ها. بیرون از پوشه‌ی نسخه.

.PARAMETER Port
    پورت http سایت. پیش‌فرض ۸۰. فقط هنگام ساختِ سایت اثر دارد؛ اگر سایت از قبل باشد
    بایندینگ‌هایش دست نمی‌خورد و فقط با این مقدار مقایسه می‌شوند.

.PARAMETER Hostname
    نام میزبانی که سایت رویش پاسخ می‌دهد (host header). بدون آن، سایت روی پورت ۸۰
    بدون host header ساخته می‌شود و با هر سایت دیگری روی آن پورت تداخل پیدا می‌کند.

.PARAMETER HttpsCertThumbprint
    اثر انگشت گواهی در Cert:\LocalMachine\My. با دادنش بایندینگ HTTPS روی ۴۴۳ ساخته
    و گواهی به آن وصل می‌شود. بدون آن به بایندینگ‌های HTTPS اصلاً دست زده نمی‌شود.

.PARAMETER ApplySchema
    اجرای Database\migrations.sql. بدون این سوئیچ، دیتابیس دست نمی‌خورد.

.PARAMETER SqlServer
    میزبان SQL Server. فقط وقتی لازم است که -ApplySchema داده شده باشد.

.PARAMETER DatabaseName
    نام دیتابیس.

.PARAMETER BackupPath
    محل نوشتن پشتیبان دیتابیس پیش از اعمال تغییر ساختار.

.PARAMETER DryRun
    فقط بررسی؛ هیچ تغییری اعمال نمی‌شود.

.EXAMPLE
    .\Deploy-Production.ps1 -PackagePath "D:\releases\HSEQ_20260823-150000" -DryRun

.EXAMPLE
    .\Deploy-Production.ps1 -PackagePath "D:\releases\HSEQ_20260823-150000" -ApplySchema -SqlServer "SQLPROD01"

.EXAMPLE
    # کنار سایت‌های موجودِ همان سرور (host header) و با HTTPS:
    .\Deploy-Production.ps1 -PackagePath "D:\releases\HSEQ_20260823-150000" `
        -Hostname 'hseq.odcc.local' -HttpsCertThumbprint 'A1B2C3...' `
        -ApplySchema -SqlServer "SQLPROD01"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,

    # پیش‌فرض‌ها = چیدمانی که واقعاً روی سرور مستقر است.
    #
    # قبلاً پیش‌فرض‌ها سایتی به نام 'HSEQ' زیر C:\Applications\HSEQ\current\ روی پورت
    # ۸۰ بود - جایی که اصلاً وجود ندارد. اجرای اسکریپت بدون پارامتر یک سایت *دوم*
    # می‌ساخت، فایل‌ها را جای دیگری می‌ریخت، و سایت واقعی دست‌نخورده می‌ماند: استقرار
    # «موفق» گزارش می‌شد در حالی که هیچ‌چیز عوض نشده بود.
    #
    # هر چهارتا همچنان پارامترند؛ برای سایت دیگری صریحاً مقدار بدهید.
    [string]$SiteName = 'HSEQTest',
    [string]$SitePath = 'D:\HouzoriApps\HSEQTest',
    [string]$ApiPath = 'D:\HouzoriApps\HSEQTest-api',
    [string]$DataPath = 'C:\ApplicationData\HSEQ',

    # بایندینگ فعلیِ آزموده‌شده. پورت، نام میزبان و طرح (http/https) همگی پارامترند
    # و هیچ‌جای کد به مقدار خاصی گره نخورده است.
    [ValidateRange(1, 65535)][int]$Port = 2525,
    [string]$Hostname = '',
    [string]$HttpsCertThumbprint = '',

    [switch]$ApplySchema,
    [string]$SqlServer,
    [string]$DatabaseName = 'HSEQDb',
    [string]$BackupPath = 'C:\ApplicationData\HSEQ\DbBackups',

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:StepNumber = 0
$script:Failures = @()
$script:Actions = @()

function Write-Step { param([string]$m) $script:StepNumber++; Write-Host ''; Write-Host ("[{0,2}] {1}" -f $script:StepNumber, $m) -ForegroundColor Cyan }
function Write-Ok   { param([string]$m) Write-Host "     OK    $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "     !     $m" -ForegroundColor Yellow }
function Write-Fail { param([string]$m) Write-Host "     FAIL  $m" -ForegroundColor Red; $script:Failures += $m }
function Write-Plan { param([string]$m) Write-Host "     PLAN  $m" -ForegroundColor Magenta; $script:Actions += $m }

Write-Host ''
Write-Host 'استقرار HSEQ' -ForegroundColor White
if ($DryRun) { Write-Host 'حالت DryRun - هیچ تغییری اعمال نمی‌شود.' -ForegroundColor Magenta }

# ---------------------------------------------------------------------------
Write-Step 'بررسی دسترسی مدیر'
# ---------------------------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    Write-Ok 'با دسترسی مدیر اجرا شده'
} elseif ($DryRun) {
    Write-Warn 'بدون دسترسی مدیر - برای DryRun کافی است، برای استقرار واقعی نه.'
} else {
    Write-Fail 'استقرار واقعی به PowerShell با دسترسی Administrator نیاز دارد.'
}

# ---------------------------------------------------------------------------
Write-Step 'اعتبارسنجی بسته'
# ---------------------------------------------------------------------------
if (-not (Test-Path $PackagePath)) { throw "مسیر بسته پیدا نشد: $PackagePath" }
$PackagePath = (Resolve-Path $PackagePath).Path

$releaseJsonPath = Join-Path $PackagePath 'release.json'
if (-not (Test-Path $releaseJsonPath)) { throw "release.json در بسته نیست: $PackagePath" }
$release = Get-Content $releaseJsonPath -Raw | ConvertFrom-Json
Write-Ok "نسخه $($release.releaseId) / کامیت $($release.gitCommit.Substring(0,8))"

if ($release.gitDirty) {
    Write-Warn 'این بسته از درخت کاری تمیز ساخته نشده (PRODUCTION_RELEASE_BLOCKED_DIRTY_WORKTREE).'
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی صحت فایل‌ها با SHA-256'
# ---------------------------------------------------------------------------
$sumsPath = Join-Path $PackagePath 'SHA256SUMS.txt'
if (-not (Test-Path $sumsPath)) {
    Write-Fail 'SHA256SUMS.txt در بسته نیست.'
} else {
    $bad = 0; $checked = 0
    foreach ($line in Get-Content $sumsPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $expectedHash, $relPath = $line -split '\s{2,}', 2
        $full = Join-Path $PackagePath $relPath
        if (-not (Test-Path $full)) { $bad++; Write-Fail "فایل غایب: $relPath"; continue }
        if ((Get-FileHash $full -Algorithm SHA256).Hash -ne $expectedHash) { $bad++; Write-Fail "هش نامعتبر: $relPath" }
        $checked++
    }
    if ($bad -eq 0) { Write-Ok "$checked فایل بررسی شد، همه سالم" }
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی پیش‌نیازهای IIS'
# ---------------------------------------------------------------------------
$iisAvailable = $false
if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
    Write-Fail 'ماژول WebAdministration نیست - یعنی IIS نصب نشده.'
} else {
    # وجود خودِ ماژول به معنای نصب بودن IIS نیست: Windows PowerShell 5.1 مانیفست
    # WebAdministration را همیشه دارد، ولی بدون نصب IIS ارائه‌دهنده‌اش ثبت نشده و
    # اولین فراخوانی خطا می‌دهد. چون $ErrorActionPreference روی Stop است، آن خطا
    # کل اسکریپت را وسط مرحله‌ی بررسی می‌کشت - یعنی دقیقاً همان استقرار نیمه‌کاره‌ای
    # که این ترتیب برای جلوگیری از آن طراحی شده. پس اینجا خطا مهار می‌شود و
    # به‌صورت یک بررسیِ ناموفقِ صریح گزارش می‌شود.
    $globalModules = $null
    try {
        Import-Module WebAdministration -ErrorAction Stop
        $globalModules = @(Get-WebGlobalModule -ErrorAction Stop)
        $iisAvailable = $true
        Write-Ok 'IIS موجود است'
    } catch {
        Write-Fail "IIS در دسترس نیست (ماژول هست ولی کار نمی‌کند): $($_.Exception.Message)"
    }

    if ($iisAvailable) {
        # ماژول ASP.NET Core - بدون آن بک‌اند اصلاً بالا نمی‌آید.
        if ($globalModules | Where-Object { $_.Name -eq 'AspNetCoreModuleV2' }) {
            Write-Ok 'AspNetCoreModuleV2 نصب است'
        } else {
            Write-Fail 'AspNetCoreModuleV2 نیست. ASP.NET Core Hosting Bundle را آفلاین نصب کنید (این اسکریپت چیزی از اینترنت دانلود نمی‌کند).'
        }

        # URL Rewrite - برای مسیرهای داخلی React لازم است، وگرنه رفرش روی /documents خطای 404 می‌دهد.
        if ($globalModules | Where-Object { $_.Name -eq 'RewriteModule' }) {
            Write-Ok 'URL Rewrite نصب است'
        } else {
            Write-Fail 'URL Rewrite نیست. بدون آن رفرش صفحه روی مسیرهای داخلی برنامه 404 می‌دهد.'
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی زمان اجرای .NET'
# ---------------------------------------------------------------------------
$runtimes = @(& dotnet --list-runtimes 2>$null)
$aspnet10 = $runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 10\.' }
if ($aspnet10) {
    Write-Ok "ASP.NET Core Runtime موجود: $(($aspnet10 | Select-Object -First 1))"
} else {
    Write-Fail 'ASP.NET Core Runtime 10 پیدا نشد. Hosting Bundle نسخه ۱۰ لازم است.'
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی نام میزبان و گواهی'
# ---------------------------------------------------------------------------
# هر دو اختیاری‌اند، ولی اگر داده شده باشند باید همین‌جا - پیش از هر تغییری - تأیید
# شوند: گواهیِ نبوده وسط پیکربندی IIS سایت را نیمه‌کاره رها می‌کند، و نامی که روی
# خود سرور resolve نمی‌شود بررسی سلامتِ پایان کار را بی‌دلیل قرمز می‌کند.
if (-not $Hostname -and -not $HttpsCertThumbprint) {
    Write-Warn "بدون -Hostname و -HttpsCertThumbprint: سایت روی http و پورت $Port بدون host header."
} else {
    if ($Hostname) {
        try {
            $null = [System.Net.Dns]::GetHostAddresses($Hostname)
            Write-Ok "نام '$Hostname' روی همین سرور resolve می‌شود"
        } catch {
            Write-Warn "نام '$Hostname' روی همین سرور resolve نمی‌شود."
            Write-Warn 'سایت ساخته می‌شود، ولی بررسی سلامتِ پایان کار به آن نمی‌رسد. یک رکورد DNS یا یک سطر در hosts لازم است.'
        }
    }

    if ($HttpsCertThumbprint) {
        # فاصله و خط تیره‌ای که هنگام کپی از پنجره‌ی گواهی ویندوز می‌آید حذف شود.
        $HttpsCertThumbprint = ($HttpsCertThumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
        $cert = Get-ChildItem -Path Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
                Where-Object { $_.Thumbprint -eq $HttpsCertThumbprint }
        if (-not $cert) {
            Write-Fail "گواهی با این اثر انگشت در Cert:\LocalMachine\My نیست: $HttpsCertThumbprint"
        } else {
            Write-Ok "گواهی پیدا شد: $($cert.Subject)"
            if (-not $cert.HasPrivateKey) {
                Write-Fail 'گواهی کلید خصوصی ندارد؛ برای بایندینگ HTTPS قابل استفاده نیست.'
            }
            if ($cert.NotAfter -lt (Get-Date)) {
                Write-Fail "گواهی در $($cert.NotAfter.ToString('yyyy-MM-dd')) منقضی شده است."
            } elseif ($cert.NotAfter -lt (Get-Date).AddDays(30)) {
                Write-Warn "گواهی در $($cert.NotAfter.ToString('yyyy-MM-dd')) منقضی می‌شود."
            }
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی تنظیمات عملیاتی'
# ---------------------------------------------------------------------------
# فایل تنظیمات عمداً جزو بسته نیست - روی سرور می‌ماند و با هر استقرار جایگزین نمی‌شود.
$prodConfig = Join-Path $ApiPath 'appsettings.Production.json'
if (Test-Path $prodConfig) {
    Write-Ok 'appsettings.Production.json روی سرور موجود است و حفظ می‌شود'
} else {
    Write-Warn "appsettings.Production.json در $ApiPath نیست."
    Write-Warn 'از Config\appsettings.Production.template.json کپی بگیرید و مقادیرش را پر کنید.'
    Write-Warn 'بدون آن برنامه با پیام صریح بالا نمی‌آید (اعتبارسنجی تنظیمات).'
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی پورت و سایت'
# ---------------------------------------------------------------------------
# بدون IIS این بررسی اصلاً معنا ندارد؛ نبودش قبلاً به‌عنوان خطا ثبت شده و اینجا
# فقط رد می‌شویم تا بقیه‌ی بررسی‌ها هم گزارش شوند و اسکریپت وسط کار نیفتد.
$existingSite = $null
if (-not $iisAvailable) {
    Write-Warn 'رد شد - IIS در دسترس نیست.'
} elseif ($existingSite = Get-Website -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq $SiteName }) {
    Write-Ok "سایت '$SiteName' موجود است - به‌روزرسانی می‌شود، ساخت مجدد نه"

    # بایندینگ‌های سایتِ موجود عمداً دست نمی‌خورند. ولی اگر هیچ‌کدام با -Port جور
    # نباشند، بررسی سلامتِ پایان کار به جای اشتباهی می‌زند و بی‌دلیل قرمز می‌شود -
    # پس همین‌جا گفته می‌شود، نه آنجا.
    $matching = @($existingSite.Bindings.Collection | Where-Object {
        ($_.bindingInformation -split ':', 3)[1] -eq "$Port"
    })
    if ($matching.Count -eq 0) {
        $actual = @($existingSite.Bindings.Collection | ForEach-Object { $_.bindingInformation }) -join ' , '
        Write-Warn "هیچ بایندینگی روی پورت $Port نیست. بایندینگ‌های فعلی: $actual"
        Write-Warn "اگر سایت روی پورت دیگری است، همان را با -Port بدهید تا بررسی سلامت درست کار کند."
    }

    # host header روی همه‌ی بایندینگ‌ها یعنی صدا زدن با IP خطای 400 می‌گیرد.
    $openBinding = @($existingSite.Bindings.Collection | Where-Object {
        $parts = $_.bindingInformation -split ':', 3
        $parts.Count -eq 3 -and $parts[1] -eq "$Port" -and -not $parts[2]
    })
    if ($matching.Count -gt 0 -and $openBinding.Count -eq 0 -and -not $Hostname) {
        Write-Warn 'همه‌ی بایندینگ‌های این پورت host header دارند؛ صدا زدن با IP خطای ۴۰۰ می‌گیرد.'
        Write-Warn 'یا -Hostname را با همان نام بدهید، یا در IIS Manager فیلد Host name را خالی کنید.'
    }
} else {
    Write-Warn "سایت '$SiteName' موجود نیست و باید ساخته شود."

    # بایندینگ سایت دیگری را نمی‌گیریم. اما «همان پورت» به‌تنهایی تداخل نیست: IIS
    # بایندینگ‌ها را با host header تفکیک می‌کند، پس سایتی روی *:80: و سایت ما روی
    # *:80:<نام میزبان> کنار هم کار می‌کنند. بدون این تفکیک، روی هر سروری که
    # Default Web Site فعال دارد استقرار همیشه شکست می‌خورد.
    # پورت ۴۴۳ فقط وقتی بررسی می‌شود که واقعاً قرار باشد بایندینگ HTTPS ساخته شود.
    $portsToCheck = @($Port)
    if ($HttpsCertThumbprint) { $portsToCheck += 443 }

    foreach ($port in $portsToCheck) {
        $conflict = @(Get-Website -ErrorAction SilentlyContinue | Where-Object {
            $_.Name -ne $SiteName -and @(
                $_.Bindings.Collection | Where-Object {
                    $parts = $_.bindingInformation -split ':', 3
                    $parts.Count -eq 3 -and $parts[1] -eq "$port" -and $parts[2] -eq $Hostname
                }
            ).Count -gt 0
        })
        if ($conflict.Count -gt 0) {
            $which = if ($Hostname) { "با نام میزبان '$Hostname'" } else { 'بدون نام میزبان' }
            Write-Fail ("پورت $port $which در اختیار سایت '$($conflict[0].Name)' است. " +
                        'بایندینگ سایت دیگری گرفته نمی‌شود - با -Hostname یک نام میزبان بدهید تا کنار آن سایت بنشیند.')
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی اتصال SQL Server'
# ---------------------------------------------------------------------------
if ($ApplySchema) {
    if (-not $SqlServer) {
        Write-Fail 'برای -ApplySchema باید -SqlServer داده شود.'
    } else {
        try {
            $conn = New-Object System.Data.SqlClient.SqlConnection
            $conn.ConnectionString = "Server=$SqlServer;Database=$DatabaseName;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
            $conn.Open()
            $conn.Close()
            Write-Ok "اتصال به $SqlServer\$DatabaseName برقرار شد"
        } catch {
            Write-Fail "اتصال به SQL برقرار نشد: $($_.Exception.Message)"
        }
    }
} else {
    Write-Warn 'بدون -ApplySchema اجرا شد؛ دیتابیس دست نمی‌خورد.'
}

# ---------------------------------------------------------------------------
Write-Step 'جمع‌بندی بررسی‌ها'
# ---------------------------------------------------------------------------
if ($script:Failures.Count -gt 0) {
    Write-Host ''
    Write-Host "استقرار متوقف شد - $($script:Failures.Count) بررسی ناموفق:" -ForegroundColor Red
    $script:Failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ''
    exit 1
}
Write-Ok 'همه‌ی بررسی‌های پیش از استقرار پاس شد'

if ($DryRun) {
    Write-Host ''
    Write-Host 'DryRun: از اینجا به بعد اجرا نشد. کارهایی که انجام می‌شد:' -ForegroundColor Magenta
    Write-Host "  - ساخت/تأیید مسیر داده‌های ماندگار: $DataPath"
    if ($ApplySchema) { Write-Host "  - پشتیبان‌گیری از $DatabaseName سپس اجرای migrations.sql" }
    Write-Host "  - نسخه‌برداری از استقرار فعلی"
    Write-Host "  - کپی Backend به $ApiPath و Frontend به $SitePath"
    Write-Host "  - تنظیم سایت '$SiteName' و Application با مسیر /api"
    if ($Hostname) { Write-Host "  - بایندینگ http روی پورت $Port با host header '$Hostname'" }
    else            { Write-Host "  - بایندینگ http روی پورت $Port بدون host header" }
    if ($HttpsCertThumbprint) { Write-Host "  - بایندینگ https روی پورت ۴۴۳ و وصل کردن گواهی" }
    Write-Host "  - ری‌سایکل فقط Application Pool مربوط به همین سایت"
    Write-Host "  - بررسی سلامت"
    Write-Host ''
    exit 0
}

# ===========================================================================
# از اینجا به بعد تغییرات واقعی
# ===========================================================================

Write-Step 'آماده‌سازی مسیر داده‌های ماندگار'
# فایل مدارک و لاگ‌ها عمداً بیرون از پوشه‌ی نسخه‌اند تا استقرار بعدی پاکشان نکند.
foreach ($dir in @($DataPath, (Join-Path $DataPath 'Documents'), (Join-Path $DataPath 'logs'), $BackupPath)) {
    if (-not (Test-Path $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
}
Write-Ok $DataPath

Write-Step 'پشتیبان‌گیری از دیتابیس'
if ($ApplySchema) {
    $backupFile = Join-Path $BackupPath ("{0}_{1}.bak" -f $DatabaseName, (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $backupSql = "BACKUP DATABASE [$DatabaseName] TO DISK = N'$backupFile' WITH INIT, COMPRESSION, STATS = 10;"
    & sqlcmd -S $SqlServer -E -C -Q $backupSql
    if ($LASTEXITCODE -ne 0) { throw 'پشتیبان‌گیری شکست خورد. تغییر ساختار اعمال نشد.' }
    if (-not (Test-Path $backupFile)) { throw 'فایل پشتیبان ساخته نشد. تغییر ساختار اعمال نشد.' }
    Write-Ok "پشتیبان: $backupFile"
} else {
    Write-Warn 'رد شد (بدون -ApplySchema).'
}

Write-Step 'اعمال تغییرات ساختار دیتابیس'
if ($ApplySchema) {
    $migrationScript = Join-Path $PackagePath 'Database\migrations.sql'
    if (-not (Test-Path $migrationScript)) { throw "اسکریپت مهاجرت در بسته نیست: $migrationScript" }
    & sqlcmd -S $SqlServer -d $DatabaseName -E -C -b -i $migrationScript
    if ($LASTEXITCODE -ne 0) { throw "اجرای مهاجرت شکست خورد. برای بازگردانی از پشتیبان بالا استفاده کنید." }
    Write-Ok 'مهاجرت اعمال شد'
} else {
    Write-Warn 'رد شد (بدون -ApplySchema).'
}

Write-Step 'نسخه‌برداری از استقرار فعلی'
# استقرار قبلی پاک نمی‌شود؛ کنار گذاشته می‌شود تا بازگشت سریع ممکن باشد.
$rollbackRoot = Join-Path $DataPath ("rollback\{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($pair in @(@{ Src = $ApiPath; Name = 'api' }, @{ Src = $SitePath; Name = 'frontend' })) {
    if (Test-Path $pair.Src) {
        $dest = Join-Path $rollbackRoot $pair.Name
        $null = New-Item -ItemType Directory -Path $dest -Force
        Copy-Item -Path (Join-Path $pair.Src '*') -Destination $dest -Recurse -Force -ErrorAction SilentlyContinue
    }
}
if (Test-Path $rollbackRoot) { Write-Ok "نسخه‌ی قابل بازگشت: $rollbackRoot" } else { Write-Warn 'استقرار قبلی وجود نداشت.' }

Write-Step 'کپی فایل‌های برنامه'
foreach ($dir in @($ApiPath, $SitePath)) {
    if (-not (Test-Path $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
}

# appsettings.Production.json روی سرور می‌ماند و با فایل‌های بسته جایگزین نمی‌شود.
$preserved = @()
if (Test-Path $prodConfig) {
    $tmp = Join-Path $env:TEMP ("hseq_prodcfg_{0}.json" -f (Get-Random))
    Copy-Item $prodConfig $tmp -Force
    $preserved += @{ Temp = $tmp; Target = $prodConfig }
}

Copy-Item -Path (Join-Path $PackagePath 'Backend\*') -Destination $ApiPath -Recurse -Force
if (Test-Path (Join-Path $PackagePath 'Frontend')) {
    Copy-Item -Path (Join-Path $PackagePath 'Frontend\*') -Destination $SitePath -Recurse -Force
}

foreach ($p in $preserved) {
    Copy-Item $p.Temp $p.Target -Force
    Remove-Item $p.Temp -Force -ErrorAction SilentlyContinue
}
Write-Ok 'فایل‌ها کپی شدند و تنظیمات عملیاتی حفظ شد'

Write-Step 'تنظیم دسترسی‌های فایل‌سیستم'
# هویت Application Pool فقط جایی که واقعاً می‌نویسد دسترسی نوشتن می‌گیرد.
$poolIdentity = "IIS AppPool\$SiteName"
foreach ($writable in @((Join-Path $DataPath 'Documents'), (Join-Path $DataPath 'logs'))) {
    try {
        $acl = Get-Acl $writable
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $poolIdentity, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.SetAccessRule($rule)
        Set-Acl $writable $acl
        Write-Ok "دسترسی نوشتن روی $writable"
    } catch {
        Write-Warn "تنظیم ACL روی $writable ناموفق: $($_.Exception.Message)"
    }
}

Write-Step 'پیکربندی IIS'
if (-not $existingSite) {
    if (-not (Test-Path "IIS:\AppPools\$SiteName")) {
        $null = New-WebAppPool -Name $SiteName
        # ASP.NET Core خودش میزبان است، پس app pool نباید CLR بارگذاری کند.
        Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ''
    }
    if ($Hostname) {
        $null = New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $SiteName -Port $Port -HostHeader $Hostname -Force
        Write-Ok "سایت '$SiteName' ساخته شد (پورت $Port، host header: $Hostname)"
    } else {
        $null = New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $SiteName -Port $Port -Force
        Write-Ok "سایت '$SiteName' ساخته شد (پورت $Port)"
    }
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $SitePath
    Write-Ok "سایت '$SiteName' به‌روزرسانی شد"
}

# بک‌اند به‌عنوان Application با مسیر /api زیر همان سایت - همان چیزی که باعث می‌شود
# کلاینت با آدرس نسبی /api کار کند و به دامنه گره نخورد.
$apiApp = Get-WebApplication -Site $SiteName -Name 'api' -ErrorAction SilentlyContinue
if (-not $apiApp) {
    $null = New-WebApplication -Site $SiteName -Name 'api' -PhysicalPath $ApiPath -ApplicationPool $SiteName
    Write-Ok "Application با مسیر /api ساخته شد"
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName\api" -Name physicalPath -Value $ApiPath
    Write-Ok "Application با مسیر /api به‌روزرسانی شد"
}

# بایندینگ HTTPS فقط وقتی که اثر انگشت داده شده باشد. بدون آن هیچ بایندینگی دست
# نمی‌خورد، پس سایتی که گواهی‌اش دستی در IIS Manager تنظیم شده با استقرار مجدد
# خراب نمی‌شود.
if ($HttpsCertThumbprint) {
    Write-Step 'بایندینگ HTTPS'

    $sslFilter = { ($_.bindingInformation -split ':', 3)[2] -eq $Hostname }
    $httpsBinding = @(Get-WebBinding -Name $SiteName -Protocol https -ErrorAction SilentlyContinue |
                      Where-Object $sslFilter)

    if ($httpsBinding.Count -eq 0) {
        if ($Hostname) {
            # SslFlags 1 یعنی SNI: چند گواهی روی همان پورت ۴۴۳، هر کدام برای یک نام.
            $null = New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $Hostname -SslFlags 1
        } else {
            $null = New-WebBinding -Name $SiteName -Protocol https -Port 443
        }
        $httpsBinding = @(Get-WebBinding -Name $SiteName -Protocol https -ErrorAction SilentlyContinue |
                          Where-Object $sslFilter)
        Write-Ok 'بایندینگ HTTPS روی پورت ۴۴۳ ساخته شد'
    } else {
        Write-Ok 'بایندینگ HTTPS از قبل موجود بود'
    }

    # AddSslCertificate روی خودِ بایندینگ - برخلاف IIS:\SslBindings، با SNI هم کار می‌کند.
    try {
        $httpsBinding[0].AddSslCertificate($HttpsCertThumbprint, 'My')
        Write-Ok "گواهی $($HttpsCertThumbprint.Substring(0, 8))... به بایندینگ وصل شد"
    } catch {
        Write-Fail "وصل کردن گواهی به بایندینگ HTTPS ناموفق: $($_.Exception.Message)"
    }
}

Write-Step 'ری‌استارت Application Pool'
# فقط app pool همین سایت. IIS به‌صورت کلی ری‌استارت نمی‌شود.
Restart-WebAppPool -Name $SiteName
Start-Sleep -Seconds 5
Write-Ok "Application Pool '$SiteName' ری‌سایکل شد"

Write-Step 'بررسی سلامت'

# وضعیت پاسخ یک نشانی، بدون پرتاب استثنا. کد HTTP خودش داده است، نه خطا: ۴۰۱ روی
# مسیر محافظت‌شده یعنی برنامه سالم بالا آمده.
function Get-HttpStatus {
    param(
        [string]$Url,
        [string]$Method = 'GET'
    )
    try {
        $r = Invoke-WebRequest -Uri $Url -Method $Method -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
        return [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

# مسیری که کلاینت واقعاً صدا می‌زند. کلاینت با VITE_API_BASE_URL=/api ساخته شده،
# پس هر فراخوانش به <origin>/api/<Controller> می‌رود.
# وقتی سایت با host header ساخته شده، درخواست به localhost اصلاً به این سایت
# نمی‌رسد - IIS آن را به سایتِ بدون host header می‌دهد. پس بررسی سلامت باید با
# همان نامی انجام شود که سایت رویش گوش می‌دهد.
$healthHost = if ($Hostname) { $Hostname } else { 'localhost' }
# پورت غیر ۸۰ باید در خودِ نشانی بیاید، وگرنه بررسی سلامت به پورت ۸۰ می‌زند -
# یعنی به سایت دیگری، یا به هیچ‌جا.
$healthBase = if ($Port -eq 80) { "http://$healthHost" } else { "http://${healthHost}:$Port" }

$clientApiHealth = "$healthBase/api/Health"
# مسیر دوتایی. پیشوند /api فقط یک مالک دارد - خودِ IIS، از راه Application زیر
# همان مسیر - پس این نشانی باید ۴۰۴ بدهد. پاسخ دادنش یعنی کنترلرها دوباره «api»
# را داخل [Route] خودشان دارند، یعنی بسته‌ای قدیمی‌تر از اصلاحِ مالکیتِ پیشوند در
# حال استقرار است.
$doubledApiHealth = "$healthBase/api/api/Health"

$clientStatus = Get-HttpStatus $clientApiHealth
$doubledStatus = Get-HttpStatus $doubledApiHealth

if ($clientStatus -eq 200) {
    Write-Ok 'بررسی سلامت پاسخ داد (۲۰۰) - دیتابیس هم در دسترس است'
}
elseif ($clientStatus -eq 503) {
    # برنامه بالا آمده ولی به دیتابیس نمی‌رسد. این دقیقاً همان خرابی‌ای است که با
    # بررسی قدیمی (۴۰۱ روی مسیر محافظت‌شده) دیده نمی‌شد.
    Write-Fail 'برنامه بالا آمد ولی به دیتابیس وصل نمی‌شود (۵۰۳). رشته‌ی اتصال و دسترسی هویت Application Pool به SQL را بررسی کنید.'
}
elseif ($doubledStatus -in 200, 503) {
    # بک‌اند بالاست، ولی زیر مسیری نشسته که کلاینت آنجا را صدا نمی‌زند. این همان
    # خرابی‌ای است که یک‌بار روی سرور دیده شد و ورود هیچ کاربری ممکن نبود.
    Write-Fail @'
ناسازگاری مسیر: بک‌اند فقط روی /api/api/... پاسخ می‌دهد، ولی کلاینت /api/... را صدا می‌زند.
با این چیدمان همه‌ی فراخوان‌های کلاینت - از جمله /api/Auth/login - خطای ۴۰۴ می‌گیرند.

یعنی این بسته قدیمی‌تر از اصلاحِ مالکیتِ پیشوند است: در آن نسخه کنترلرها هنوز
[Route("api/[controller]")] داشتند و پیشوند دو مالک داشت.
بسته‌ی به‌روز را منتشر (Publish-Production.ps1) و دوباره استقرار دهید.
'@
}
elseif ($clientStatus -eq 404) {
    Write-Fail 'نشانی سلامت پاسخ نداد (۴۰۴). بک‌اند بالا نیامده یا مسیر Application اشتباه است.'
}
else {
    # بسته‌های قدیمی‌تر HealthController را ندارند؛ در آن حالت مسیر محافظت‌شده هنوز
    # نشان می‌دهد که برنامه بالا آمده است.
    $legacyStatus = Get-HttpStatus "$healthBase/api/MasterData/projects"
    if ($legacyStatus -eq 401) {
        Write-Warn 'HealthController در این بسته نیست؛ فقط تأیید شد که برنامه بالا آمده (۴۰۱ روی مسیر محافظت‌شده). اتصال دیتابیس بررسی نشد.'
    } else {
        Write-Fail "بررسی سلامت ناموفق (کد $clientStatus روی $clientApiHealth)."
    }
}

if ($doubledStatus -notin 404, 0) {
    Write-Fail "مسیر دوتایی /api/api/Health با کد $doubledStatus پاسخ داد؛ باید ۴۰۴ باشد. پیشوند /api دو مالک دارد."
}
else {
    Write-Ok 'مسیر دوتایی /api/api/... پاسخ نمی‌دهد - پیشوند فقط یک مالک دارد'
}

# ---------------------------------------------------------------------------
# خودِ مسیر ورود، نه فقط مسیر سلامت.
#
# چرا جداگانه: بررسی سلامت روی HealthController است و ممکن است سالم باشد در حالی
# که مسیر ورود نباشد. خرابی‌ای که روی سرور دیده شد دقیقاً همین شکل بود - کاربر فقط
# در لحظه‌ی ورود با ۴۰۴ روبه‌رو می‌شد.
#
# بدنه عمداً خالی فرستاده می‌شود: هیچ اعتبارنامه‌ای لازم نیست و هیچ تلاشِ ورودی هم
# ثبت نمی‌شود. چیزی که اهمیت دارد این است که پاسخ ۴۰۴ *نباشد* - یعنی درخواست به
# اکشن رسیده. اعتبارسنجی ورودی بعد از آن ۴۰۰ می‌دهد که همان پاسخ درست است.
$loginStatus = Get-HttpStatus "$healthBase/api/Auth/login" -Method 'POST'

if ($loginStatus -eq 404) {
    Write-Fail 'مسیر ورود /api/Auth/login خطای ۴۰۴ می‌دهد - هیچ کاربری نمی‌تواند وارد شود.'
}
elseif ($loginStatus -ge 500) {
    Write-Fail "مسیر ورود /api/Auth/login کد $loginStatus داد. لاگ stdout بک‌اند را ببینید."
}
elseif ($loginStatus -eq 0) {
    Write-Fail 'مسیر ورود /api/Auth/login اصلاً پاسخ نداد.'
}
else {
    Write-Ok "مسیر ورود /api/Auth/login پاسخ داد (کد $loginStatus) - به اکشن می‌رسد"
}

try {
    $spa = Invoke-WebRequest -Uri "$healthBase/" -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
    if ($spa.Content -match '<div id="root"') { Write-Ok 'کلاینت سرو می‌شود' }
    else { Write-Warn 'ریشه پاسخ داد ولی محتوایش شبیه کلاینت نیست.' }
} catch {
    Write-Fail "کلاینت سرو نشد: $($_.Exception.Message)"
}

Write-Host ''
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
if ($script:Failures.Count -eq 0) {
    Write-Host 'استقرار انجام شد.' -ForegroundColor Green
    Write-Host "بازگشت به نسخه‌ی قبل: محتوای $rollbackRoot را روی مسیرهای مقصد برگردانید و app pool را ری‌سایکل کنید."
    if ($ApplySchema) { Write-Host 'بازگردانی دیتابیس فقط از طریق restore پشتیبان بالا - به‌صورت خودکار انجام نمی‌شود.' }
    exit 0
} else {
    Write-Host "استقرار با $($script:Failures.Count) خطا تمام شد." -ForegroundColor Red
    Write-Host "برای بازگشت: محتوای $rollbackRoot را روی مسیرهای مقصد برگردانید."
    exit 1
}
