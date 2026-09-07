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
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,

    [string]$SiteName = 'HSEQ',
    [string]$SitePath = 'C:\Applications\HSEQ\current\frontend',
    [string]$ApiPath = 'C:\Applications\HSEQ\current\api',
    [string]$DataPath = 'C:\ApplicationData\HSEQ',

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
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $iisAvailable = $true
    Write-Ok 'IIS موجود است'

    # ماژول ASP.NET Core - بدون آن بک‌اند اصلاً بالا نمی‌آید.
    $aspNetCore = Get-WebGlobalModule -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'AspNetCoreModuleV2' }
    if ($aspNetCore) {
        Write-Ok 'AspNetCoreModuleV2 نصب است'
    } else {
        Write-Fail 'AspNetCoreModuleV2 نیست. ASP.NET Core Hosting Bundle را آفلاین نصب کنید (این اسکریپت چیزی از اینترنت دانلود نمی‌کند).'
    }

    # URL Rewrite - برای مسیرهای داخلی React لازم است، وگرنه رفرش روی /documents خطای 404 می‌دهد.
    $rewrite = Get-WebGlobalModule -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'RewriteModule' }
    if ($rewrite) {
        Write-Ok 'URL Rewrite نصب است'
    } else {
        Write-Fail 'URL Rewrite نیست. بدون آن رفرش صفحه روی مسیرهای داخلی برنامه 404 می‌دهد.'
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
} else {
    Write-Warn "سایت '$SiteName' موجود نیست و باید ساخته شود."
    # پورت را از سایت دیگری نمی‌گیریم.
    foreach ($port in 80, 443) {
        $conflict = Get-Website -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne $SiteName -and $_.Bindings.Collection.bindingInformation -match ":${port}:" }
        if ($conflict) {
            Write-Fail "پورت $port در اختیار سایت '$($conflict.Name)' است. این اسکریپت پورت سایت دیگری را نمی‌گیرد."
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
    $null = New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $SiteName -Port 80 -Force
    Write-Ok "سایت '$SiteName' ساخته شد"
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

Write-Step 'ری‌استارت Application Pool'
# فقط app pool همین سایت. IIS به‌صورت کلی ری‌استارت نمی‌شود.
Restart-WebAppPool -Name $SiteName
Start-Sleep -Seconds 5
Write-Ok "Application Pool '$SiteName' ری‌سایکل شد"

Write-Step 'بررسی سلامت'

# وضعیت پاسخ یک نشانی، بدون پرتاب استثنا. کد HTTP خودش داده است، نه خطا: ۴۰۱ روی
# مسیر محافظت‌شده یعنی برنامه سالم بالا آمده.
function Get-HttpStatus {
    param([string]$Url)
    try {
        $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
        return [int]$r.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

# مسیری که کلاینت واقعاً صدا می‌زند. کلاینت با VITE_API_BASE_URL=/api ساخته شده،
# پس هر فراخوانش به <origin>/api/<Controller> می‌رود.
$clientApiHealth = 'http://localhost/api/Health'
# مسیری که اگر بک‌اند به‌عنوان Application زیر /api ثبت شده باشد پاسخ می‌دهد: IIS
# پیشوند /api را به‌عنوان PathBase برمی‌دارد و مسیرِ خودِ کنترلر ("api/Health") بعد
# از آن می‌آید، یعنی نشانی نهایی دوبار /api دارد.
$pathBaseApiHealth = 'http://localhost/api/api/Health'

$clientStatus = Get-HttpStatus $clientApiHealth
$pathBaseStatus = Get-HttpStatus $pathBaseApiHealth

if ($clientStatus -eq 200) {
    Write-Ok 'بررسی سلامت پاسخ داد (۲۰۰) - دیتابیس هم در دسترس است'
    if ($pathBaseStatus -eq 200) {
        Write-Warn 'هر دو نشانی /api/Health و /api/api/Health پاسخ می‌دهند - چیدمان را بازبینی کنید.'
    }
}
elseif ($clientStatus -eq 503) {
    # برنامه بالا آمده ولی به دیتابیس نمی‌رسد. این دقیقاً همان خرابی‌ای است که با
    # بررسی قدیمی (۴۰۱ روی مسیر محافظت‌شده) دیده نمی‌شد.
    Write-Fail 'برنامه بالا آمد ولی به دیتابیس وصل نمی‌شود (۵۰۳). رشته‌ی اتصال و دسترسی هویت Application Pool به SQL را بررسی کنید.'
}
elseif ($pathBaseStatus -in 200, 503) {
    # این حالت یعنی بک‌اند بالاست، ولی زیر مسیری نشسته که کلاینت آنجا را صدا نمی‌زند.
    Write-Fail @'
ناسازگاری مسیر: بک‌اند فقط روی /api/api/... پاسخ می‌دهد، ولی کلاینت /api/... را صدا می‌زند.
با این چیدمان همه‌ی فراخوان‌های کلاینت ۴۰۴ می‌گیرند.

این حالت باید با میان‌افزارِ PathBase در Program.cs پوشش داده شده باشد؛ دیدنش یعنی
آن میان‌افزار برداشته شده یا بسته‌ای قدیمی‌تر از آن اصلاح در حال استقرار است.
بسته‌ی به‌روز را منتشر و دوباره استقرار دهید.
'@
}
elseif ($clientStatus -eq 404 -and $pathBaseStatus -eq 404) {
    Write-Fail 'هیچ‌کدام از نشانی‌های سلامت پاسخ ندادند (۴۰۴). بک‌اند بالا نیامده یا مسیر Application اشتباه است.'
}
else {
    # بسته‌های قدیمی‌تر HealthController را ندارند؛ در آن حالت مسیر محافظت‌شده هنوز
    # نشان می‌دهد که برنامه بالا آمده است.
    $legacyStatus = Get-HttpStatus 'http://localhost/api/MasterData/projects'
    if ($legacyStatus -eq 401) {
        Write-Warn 'HealthController در این بسته نیست؛ فقط تأیید شد که برنامه بالا آمده (۴۰۱ روی مسیر محافظت‌شده). اتصال دیتابیس بررسی نشد.'
    } else {
        Write-Fail "بررسی سلامت ناموفق (کد $clientStatus روی $clientApiHealth)."
    }
}

try {
    $spa = Invoke-WebRequest -Uri 'http://localhost/' -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
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
