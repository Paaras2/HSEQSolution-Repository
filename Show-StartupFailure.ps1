<#
.SYNOPSIS
    پیدا کردن علت خطای 500.30 - یعنی وقتی برنامه اصلاً بالا نمی‌آید.

.DESCRIPTION
    خطای 500.30 یعنی ماژول ASP.NET Core فرآیند را اجرا کرد و فرآیند مُرد. نه
    مسیریابی، نه ورود، نه دیتابیس - برنامه هیچ‌وقت به آن مرحله‌ها نرسیده.

    علت همیشه جایی نوشته شده، ولی معمولاً نه جایی که اول نگاه می‌کنند:

      ۱) لاگ رویداد ویندوز. ماژول ASP.NET Core هر شکستِ راه‌اندازی را آنجا ثبت
         می‌کند، *بدون* نیاز به هیچ تنظیمی. این مطمئن‌ترین منبع است و همیشه هست.

      ۲) لاگ stdout. متن کامل استثنا را دارد، ولی سه شرط لازم دارد که هر کدام
         نباشد فایل اصلاً ساخته نمی‌شود و بی‌سروصدا هیچ نمی‌شود:
            - stdoutLogEnabled="true" در web.config
            - پوشه‌ی مقصد از قبل وجود داشته باشد  ← ماژول خودش نمی‌سازدش
            - هویت Application Pool روی آن پوشه دسترسی نوشتن داشته باشد
         دومی متداول‌ترین علتِ «لاگ روشن است ولی فایلی نیست» است.

      ۳) لاگ خودِ ماژول (ANCM debug). وقتی حتی فرآیند هم شروع نمی‌شود - مثلاً
         Hosting Bundle نصب نیست - تنها چیزی است که حرف می‌زند.

    این اسکریپت هر سه را می‌خواند و گزارش می‌دهد. بدون -Fix هیچ چیزی را تغییر
    نمی‌دهد.

.PARAMETER SiteName
    نام سایت IIS.

.PARAMETER SitePath
    مسیر فیزیکی سایت (جایی که web.config و HSEQ.API.dll هستند).

.PARAMETER Fix
    پوشه‌ی لاگ را می‌سازد و دسترسی نوشتن به هویت Application Pool می‌دهد، سپس
    app pool را ری‌سایکل می‌کند. تنها تغییری که این اسکریپت اعمال می‌کند همین است.

.PARAMETER EnableModuleLog
    لاگ سطح-ماژول (ANCM) را در web.config روشن می‌کند. برای وقتی که حتی stdout
    هم خالی است. پس از عیب‌یابی با -DisableModuleLog خاموشش کنید.

.PARAMETER DisableModuleLog
    لاگ سطح-ماژول را خاموش و تنظیماتش را از web.config برمی‌دارد.

.EXAMPLE
    .\Show-StartupFailure.ps1
    فقط گزارش می‌دهد.

.EXAMPLE
    .\Show-StartupFailure.ps1 -Fix
    پوشه‌ی لاگ و دسترسی‌اش را درست می‌کند، ری‌سایکل می‌کند، بعد گزارش می‌دهد.
#>
[CmdletBinding()]
param(
    [string]$SiteName = 'HSEQTest',
    [string]$SitePath = 'D:\HouzoriApps\HSEQTest',
    [switch]$Fix,
    [switch]$EnableModuleLog,
    [switch]$DisableModuleLog
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$step = 0
function Write-Step { param([string]$m) $script:step++; Write-Host ''; Write-Host ("[{0}] {1}" -f $script:step, $m) -ForegroundColor Cyan }
function Write-Ok   { param([string]$m) Write-Host "    OK    $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "    !     $m" -ForegroundColor Yellow }
function Write-Fail { param([string]$m) Write-Host "    FAIL  $m" -ForegroundColor Red }
function Write-Info { param([string]$m) Write-Host "          $m" -ForegroundColor DarkGray }

Write-Host ''
Write-Host 'عیب‌یابی شکستِ راه‌اندازی (500.30)' -ForegroundColor White

# ---------------------------------------------------------------------------
Write-Step 'پیش‌نیازها'
# ---------------------------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) { Write-Ok 'با دسترسی مدیر' }
else { Write-Warn 'بدون دسترسی مدیر - لاگ رویداد و -Fix ممکن است کار نکنند.' }

$webConfigPath = Join-Path $SitePath 'web.config'
if (-not (Test-Path $webConfigPath)) {
    Write-Fail "web.config در $SitePath نیست. مسیر سایت درست است؟"
    Write-Info 'با -SitePath مسیر درست را بدهید.'
    exit 1
}
Write-Ok "web.config: $webConfigPath"

if (-not (Test-Path (Join-Path $SitePath 'HSEQ.API.dll'))) {
    Write-Fail 'HSEQ.API.dll کنار web.config نیست - محتویات Site\ کامل کپی نشده.'
}

# ---------------------------------------------------------------------------
Write-Step 'تنظیمات لاگ در web.config'
# ---------------------------------------------------------------------------
[xml]$webConfig = Get-Content $webConfigPath -Raw
$aspNetCore = $webConfig.SelectSingleNode('//aspNetCore')
if (-not $aspNetCore) {
    Write-Fail 'بخش <aspNetCore> در web.config نیست.'
    exit 1
}

$stdoutEnabled = $aspNetCore.GetAttribute('stdoutLogEnabled')
$stdoutFile = $aspNetCore.GetAttribute('stdoutLogFile')
$hostingModel = $aspNetCore.GetAttribute('hostingModel')

Write-Info "stdoutLogEnabled : $stdoutEnabled"
Write-Info "stdoutLogFile    : $stdoutFile"
Write-Info "hostingModel     : $hostingModel"

if ($stdoutEnabled -ne 'true') {
    Write-Warn 'لاگ stdout خاموش است. برای دیدن استثنای راه‌اندازی روشنش کنید.'
}

# مسیرِ نسبی (".\logs\stdout") نسبت به پوشه‌ی برنامه حساب می‌شود.
$logDir = $null
if ($stdoutFile) {
    $logDir = Split-Path -Parent $stdoutFile
    if (-not [System.IO.Path]::IsPathRooted($logDir)) {
        $logDir = Join-Path $SitePath $logDir
    }
}

# ---------------------------------------------------------------------------
Write-Step 'پوشه‌ی لاگ و دسترسی نوشتن'
# ---------------------------------------------------------------------------
# این همان جایی است که معمولاً می‌شکند: ماژول ASP.NET Core پوشه را *نمی‌سازد*.
# اگر نباشد، لاگ بی‌سروصدا نوشته نمی‌شود و هیچ خطایی هم نمی‌دهد - یعنی دقیقاً
# وقتی که به لاگ نیاز دارید، خالی است.
$poolIdentity = "IIS AppPool\$SiteName"

if (-not $logDir) {
    Write-Warn 'مسیر لاگ از web.config خوانده نشد.'
}
elseif (Test-Path $logDir) {
    Write-Ok "پوشه‌ی لاگ هست: $logDir"

    $canWrite = $false
    try {
        $acl = Get-Acl $logDir
        $canWrite = @($acl.Access | Where-Object {
            $_.IdentityReference -like "*$SiteName*" -or $_.IdentityReference -like '*IIS_IUSRS*'
        }).Count -gt 0
    } catch { }

    if ($canWrite) { Write-Ok "دسترسی نوشتن برای $poolIdentity به نظر برقرار است" }
    else { Write-Warn "دسترسی نوشتن برای $poolIdentity پیدا نشد - لاگ نوشته نمی‌شود." }
}
else {
    Write-Fail "پوشه‌ی لاگ وجود ندارد: $logDir"
    Write-Info 'ماژول ASP.NET Core این پوشه را نمی‌سازد. تا وقتی نباشد، لاگ stdout'
    Write-Info 'بی‌سروصدا نوشته نمی‌شود - و این متداول‌ترین علتِ «لاگ روشن است ولی فایلی نیست».'
    Write-Info 'برای ساختنش: این اسکریپت را با -Fix اجرا کنید.'
}

if ($Fix -and $logDir) {
    if (-not (Test-Path $logDir)) {
        $null = New-Item -ItemType Directory -Path $logDir -Force
        Write-Ok "پوشه ساخته شد: $logDir"
    }
    & icacls $logDir /grant "${poolIdentity}:(OI)(CI)M" | Out-Null
    Write-Ok "دسترسی Modify به $poolIdentity داده شد"
}

# ---------------------------------------------------------------------------
Write-Step 'لاگ سطح-ماژول (ANCM)'
# ---------------------------------------------------------------------------
$moduleLogPath = if ($logDir) { Join-Path $logDir 'ancm.log' } else { $null }

if ($EnableModuleLog -or $DisableModuleLog) {
    $handlerSettings = $aspNetCore.SelectSingleNode('handlerSettings')
    if ($handlerSettings) { $null = $aspNetCore.RemoveChild($handlerSettings) }

    if ($EnableModuleLog) {
        $handlerSettings = $webConfig.CreateElement('handlerSettings')
        foreach ($pair in @(@{ n = 'debugLevel'; v = 'FILE,TRACE' }, @{ n = 'debugFile'; v = $moduleLogPath })) {
            $setting = $webConfig.CreateElement('handlerSetting')
            $setting.SetAttribute('name', $pair.n)
            $setting.SetAttribute('value', $pair.v)
            $null = $handlerSettings.AppendChild($setting)
        }
        $null = $aspNetCore.AppendChild($handlerSettings)
        $webConfig.Save($webConfigPath)
        Write-Ok "روشن شد. لاگ ماژول: $moduleLogPath"
        Write-Warn 'پس از عیب‌یابی با -DisableModuleLog خاموشش کنید؛ این لاگ هم چرخش ندارد.'
    } else {
        $webConfig.Save($webConfigPath)
        Write-Ok 'لاگ سطح-ماژول خاموش شد.'
    }
}
elseif ($moduleLogPath -and (Test-Path $moduleLogPath)) {
    Write-Ok "لاگ ماژول موجود است: $moduleLogPath"
}
else {
    Write-Info 'خاموش است. اگر stdout هم خالی ماند، با -EnableModuleLog روشنش کنید.'
}

# ---------------------------------------------------------------------------
if ($Fix) {
    Write-Step 'ری‌سایکل Application Pool'
    # ---------------------------------------------------------------------------
    try {
        Import-Module WebAdministration -ErrorAction Stop
        if (Test-Path "IIS:\AppPools\$SiteName") {
            Restart-WebAppPool -Name $SiteName
            Write-Ok "app pool '$SiteName' ری‌سایکل شد"
            Write-Info 'حالا یک‌بار سایت را در مرورگر باز کنید تا برنامه دوباره تلاش کند بالا بیاید.'
            Start-Sleep -Seconds 3
        } else {
            Write-Warn "app pool '$SiteName' پیدا نشد."
        }
    } catch {
        Write-Warn "ری‌سایکل انجام نشد: $($_.Exception.Message)"
    }
}

# ---------------------------------------------------------------------------
Write-Step 'لاگ رویداد ویندوز - مطمئن‌ترین منبع'
# ---------------------------------------------------------------------------
# این یکی همیشه هست و هیچ تنظیمی نمی‌خواهد. برای 500.30 معمولاً همین کافی است.
$providers = 'IIS AspNetCore Module V2', 'IIS AspNetCore Module', '.NET Runtime', 'Application Error'
$found = 0
foreach ($provider in $providers) {
    try {
        $events = Get-WinEvent -FilterHashtable @{
            LogName = 'Application'; ProviderName = $provider; StartTime = (Get-Date).AddHours(-2)
        } -ErrorAction Stop | Select-Object -First 3

        foreach ($e in $events) {
            $found++
            Write-Host ''
            Write-Host "    ── $provider  |  $($e.TimeCreated)" -ForegroundColor Yellow
            ($e.Message -split "`n" | Select-Object -First 12) | ForEach-Object { Write-Host "       $($_.TrimEnd())" }
        }
    } catch {
        # این منبع رویدادی در دو ساعت اخیر ندارد - طبیعی است.
    }
}
if ($found -eq 0) {
    Write-Info 'رویدادی در دو ساعت گذشته نبود. اگر خطا قدیمی‌تر است، بازه را در Event Viewer بیشتر کنید،'
    Write-Info 'یا با -Fix اجرا کنید تا ری‌سایکل شود و خطا دوباره ثبت گردد.'
}

# ---------------------------------------------------------------------------
Write-Step 'تازه‌ترین لاگ stdout'
# ---------------------------------------------------------------------------
if ($logDir -and (Test-Path $logDir)) {
    $latest = Get-ChildItem $logDir -Filter 'stdout*' -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Ok "$($latest.Name)  ($($latest.LastWriteTime))"
        Write-Host ''
        Get-Content $latest.FullName -Tail 40 -Encoding UTF8 | ForEach-Object { Write-Host "       $_" }
    } else {
        Write-Warn 'هیچ فایل stdout ای نوشته نشده.'
        Write-Info 'یعنی یکی از این سه: stdoutLogEnabled=false، پوشه وجود ندارد، یا دسترسی نوشتن نیست.'
        Write-Info 'با -Fix دو مورد آخر درست می‌شوند.'
    }
} else {
    Write-Warn 'پوشه‌ی لاگ در دسترس نیست.'
}

if ($moduleLogPath -and (Test-Path $moduleLogPath)) {
    Write-Step 'لاگ ماژول (ANCM)'
    Get-Content $moduleLogPath -Tail 30 | ForEach-Object { Write-Host "       $_" }
}

# ---------------------------------------------------------------------------
Write-Step 'علت‌های متداول 500.30'
# ---------------------------------------------------------------------------
Write-Host @'
       در پیام‌های بالا دنبال این‌ها بگردید:

       «تنظیمات محیط 'Production' معتبر نیست»
           appsettings.Production.json کنار HSEQ.API.dll نیست یا مقادیرش پر نشده.
           این فایل جزو بسته نیست و باید جداگانه کپی شود.

       Could not load file or assembly / FrameworkNotFound
           ASP.NET Core 10 Hosting Bundle روی سرور نصب نیست یا نسخه‌اش قدیمی است.
           پس از نصبش حتماً IIS را ری‌استارت کنید: iisreset

       UnauthorizedAccessException روی مسیر فایل
           هویت Application Pool روی UploadPath یا پوشه‌ی لاگ دسترسی نوشتن ندارد.

       خطای اتصال SQL هنگام راه‌اندازی
           فقط وقتی برنامه را می‌کُشد که Database:MigrateOnStartup=true باشد.
           در تنظیمات عملیاتی باید false بماند.
'@ -ForegroundColor DarkGray

Write-Host ''
