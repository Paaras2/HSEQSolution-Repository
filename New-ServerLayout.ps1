<#
.SYNOPSIS
    چیدن خروجی بسته به همان شکلی که باید روی سرور روی دیسک بنشیند.

.DESCRIPTION
    بسته‌ی انتشار به تفکیک نقش چیده شده (Backend\ ، Frontend\ ، Database\ ...)، نه به
    شکلی که روی سرور لازم است. تبدیلش دو نکته دارد که هر دو خطای خاموش می‌دهند:

      - محتویاتِ Frontend\ باید در ریشه‌ی پوشه‌ی سایت بنشیند، نه خودِ پوشه. اگر
        index.html یک لایه پایین‌تر بیفتد، IIS خطای 403.14 می‌دهد.
      - بک‌اند باید بیرون از پوشه‌ی کلاینت باشد، وگرنه appsettings.Production.json
        با کلید JWT از راه وب قابل دانلود است.

    این اسکریپت همان چیدمان نهایی را می‌سازد: دو پوشه که مستقیماً در پوشه‌ی مقصد
    سرور کپی می‌شوند، بدون هیچ جابه‌جایی دستی.

    خروجی هیچ‌وقت داخل بسته‌ی امضاشده نوشته نمی‌شود تا SHA256SUMS.txt معتبر بماند.

.PARAMETER PackagePath
    پوشه‌ی بسته (همان که release.json دارد). اگر ندهید، تازه‌ترین بسته‌ی artifacts.

.PARAMETER SiteFolderName
    نام پوشه‌ی کلاینت، همان‌طور که روی سرور هست.

.PARAMETER ApiFolderName
    نام پوشه‌ی بک‌اند، همان‌طور که روی سرور هست.

.PARAMETER ConfigFile
    appsettings.Production.json که کنار HSEQ.API.dll گذاشته می‌شود.

.PARAMETER OutputPath
    محل ساخت. پیش‌فرض: artifacts\<releaseId>_ServerReady

.EXAMPLE
    .\New-ServerLayout.ps1
#>
[CmdletBinding()]
param(
    [string]$PackagePath = '',
    [string]$SiteFolderName = 'HSEQTest',
    [string]$ApiFolderName = 'HSEQTest-api',
    [string]$ConfigFile = '',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$step = 0
function Write-Step { param([string]$m) $script:step++; Write-Host ''; Write-Host ("[{0}] {1}" -f $script:step, $m) -ForegroundColor Cyan }
function Write-Ok   { param([string]$m) Write-Host "    OK  $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "    !   $m" -ForegroundColor Yellow }

Write-Host ''
Write-Host 'ساخت چیدمان آماده‌ی سرور' -ForegroundColor White

# ---------------------------------------------------------------------------
Write-Step 'یافتن بسته'
# ---------------------------------------------------------------------------
if (-not $PackagePath) {
    $artifacts = Join-Path $root 'artifacts'
    if (-not (Test-Path $artifacts)) { throw "پوشه‌ی artifacts نیست. اول Publish-Production.ps1 را اجرا کنید." }
    $candidate = Get-ChildItem $artifacts -Directory |
                 Where-Object { Test-Path (Join-Path $_.FullName 'release.json') } |
                 Sort-Object Name | Select-Object -Last 1
    if (-not $candidate) { throw "بسته‌ای در artifacts نیست. اول Publish-Production.ps1 را اجرا کنید." }
    $PackagePath = $candidate.FullName
}
if (-not (Test-Path (Join-Path $PackagePath 'release.json'))) {
    throw "این پوشه بسته نیست (release.json ندارد): $PackagePath"
}
$PackagePath = (Resolve-Path $PackagePath).Path
$release = Get-Content (Join-Path $PackagePath 'release.json') -Raw | ConvertFrom-Json
Write-Ok "$($release.releaseId) / کامیت $($release.gitCommit.Substring(0,8)) / وضعیت $($release.releaseState)"
if ($release.gitDirty) { Write-Warn 'این بسته از درخت کاری تمیز ساخته نشده.' }

foreach ($needed in 'Frontend', 'Backend') {
    if (-not (Test-Path (Join-Path $PackagePath $needed))) { throw "بسته ناقص است - $needed\ ندارد." }
}

# ---------------------------------------------------------------------------
Write-Step 'آماده‌سازی پوشه‌ی خروجی'
# ---------------------------------------------------------------------------
if (-not $OutputPath) {
    $OutputPath = Join-Path (Join-Path $root 'artifacts') ("{0}_ServerReady" -f $release.releaseId)
}
# عمداً بیرون از بسته: نوشتن داخل آن، SHA256SUMS.txt را بی‌اعتبار می‌کند.
# مقایسه با جداکننده‌ی مسیر، نه پیشوند خالی: بدون آن، پوشه‌ی هم‌نامِ کناری
# (مثل <releaseId>_ServerReady) هم به‌اشتباه «داخل بسته» تشخیص داده می‌شد.
if ($OutputPath -eq $PackagePath -or $OutputPath -like (Join-Path $PackagePath '*')) {
    throw 'مسیر خروجی نباید داخل خودِ بسته باشد.'
}

if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }
$null = New-Item -ItemType Directory -Path $OutputPath -Force

$siteOut = Join-Path $OutputPath $SiteFolderName
$apiOut  = Join-Path $OutputPath $ApiFolderName
$null = New-Item -ItemType Directory -Path $siteOut -Force
$null = New-Item -ItemType Directory -Path $apiOut -Force
Write-Ok $OutputPath

# ---------------------------------------------------------------------------
Write-Step "کلاینت → $SiteFolderName\"
# ---------------------------------------------------------------------------
Copy-Item -Path (Join-Path $PackagePath 'Frontend\*') -Destination $siteOut -Recurse -Force
$indexAtRoot = Test-Path (Join-Path $siteOut 'index.html')
if (-not $indexAtRoot) { throw 'index.html در ریشه‌ی پوشه‌ی سایت ننشست - چیدمان درست نیست.' }
Write-Ok "index.html در ریشه است (همین جلوی خطای 403.14 را می‌گیرد)"
if (Test-Path (Join-Path $siteOut 'web.config')) { Write-Ok 'web.config مسیریابی SPA موجود است' }
else { Write-Warn 'web.config نیست - رفرش روی مسیرهای داخلی ۴۰۴ می‌دهد.' }

# ---------------------------------------------------------------------------
Write-Step "بک‌اند → $ApiFolderName\"
# ---------------------------------------------------------------------------
Copy-Item -Path (Join-Path $PackagePath 'Backend\*') -Destination $apiOut -Recurse -Force
if (-not (Test-Path (Join-Path $apiOut 'HSEQ.API.dll'))) { throw 'HSEQ.API.dll کپی نشد.' }
Write-Ok 'HSEQ.API.dll و web.config موجودند'

# ---------------------------------------------------------------------------
Write-Step 'فایل تنظیمات'
# ---------------------------------------------------------------------------
if (-not $ConfigFile) {
    $default = Join-Path $root 'deploy\appsettings.Production.json'
    if (Test-Path $default) { $ConfigFile = $default }
}

$configPlaceholders = @()
if ($ConfigFile -and (Test-Path $ConfigFile)) {
    Copy-Item $ConfigFile (Join-Path $apiOut 'appsettings.Production.json') -Force
    Write-Ok "کنار HSEQ.API.dll گذاشته شد"

    $cfgRaw = Get-Content (Join-Path $apiOut 'appsettings.Production.json') -Raw
    foreach ($m in [regex]::Matches($cfgRaw, '<[A-Za-z0-9_-]+>')) {
        if ($configPlaceholders -notcontains $m.Value) { $configPlaceholders += $m.Value }
    }
    if ($configPlaceholders.Count -gt 0) {
        Write-Warn "هنوز جای‌نگهدار دارد: $($configPlaceholders -join ' , ')"
    } else {
        Write-Ok 'همه‌ی مقادیر پر شده‌اند'
    }
} else {
    Copy-Item (Join-Path $PackagePath 'Config\appsettings.Production.template.json') `
              (Join-Path $apiOut 'appsettings.Production.json') -Force
    Write-Warn 'فایل تنظیمات آماده نبود؛ قالب کپی شد و باید پر شود.'
    $configPlaceholders += '<همه‌ی مقادیر قالب>'
}

# بک‌اند نباید زیر پوشه‌ی کلاینت باشد - وگرنه کلید JWT از راه وب خواندنی است.
# باز هم مقایسه با جداکننده: 'HSEQTest-api' با پیشوند خالی، زیرمجموعه‌ی
# 'HSEQTest' به نظر می‌رسد در حالی که پوشه‌ی خواهرِ آن است.
if ($apiOut -eq $siteOut -or $apiOut -like (Join-Path $siteOut '*')) {
    throw 'پوشه‌ی بک‌اند داخل پوشه‌ی کلاینت است؛ این چیدمان کلید JWT را افشا می‌کند.'
}
Write-Ok 'بک‌اند بیرون از پوشه‌ی کلاینت است'

# ---------------------------------------------------------------------------
Write-Step 'راهنمای کپی'
# ---------------------------------------------------------------------------
$readme = @"
چیدمان آماده‌ی سرور - $($release.releaseId)
کامیت $($release.gitCommit.Substring(0,8))

این دو پوشه دقیقاً همان چیزی هستند که باید روی سرور روی دیسک باشند.

  $SiteFolderName\      →  D:\HouzoriApps\$SiteFolderName
  $ApiFolderName\  →  D:\HouzoriApps\$ApiFolderName

هر دو پوشه را با هم انتخاب کنید و در D:\HouzoriApps\ رها کنید. اگر پرسید فایل‌های
موجود جایگزین شوند، بله.

هیچ جابه‌جایی دیگری لازم نیست: index.html همین حالا در ریشه‌ی $SiteFolderName\ است.

--------------------------------------------------------------------
بعد از کپی، روی سرور:

1) دسترسی خواندن برای IIS روی پوشه‌ی کلاینت:
   icacls "D:\HouzoriApps\$SiteFolderName" /grant "IIS_IUSRS:(OI)(CI)RX" /T

2) دسترسی خواندن برای IIS روی پوشه‌ی بک‌اند:
   icacls "D:\HouzoriApps\$ApiFolderName" /grant "IIS_IUSRS:(OI)(CI)RX" /T

3) مسیر مدارک و لاگ، با دسترسی نوشتن برای app pool:
   mkdir C:\ApplicationData\HSEQ\Documents
   mkdir C:\ApplicationData\HSEQ\logs
   icacls "C:\ApplicationData\HSEQ\Documents" /grant "IIS AppPool\<APPPOOL>:(OI)(CI)M"
   icacls "C:\ApplicationData\HSEQ\logs"      /grant "IIS AppPool\<APPPOOL>:(OI)(CI)M"

4) در IIS Manager:
   - مسیر فیزیکی سایت  →  D:\HouzoriApps\$SiteFolderName
   - روی سایت راست‌کلیک → Add Application
       Alias          : api
       Physical path  : D:\HouzoriApps\$ApiFolderName
       Application pool: همان app pool سایت
   - app pool → Basic Settings → .NET CLR Version = No Managed Code
   - در بایندینگ سایت، فیلد Host name را خالی بگذارید تا با IP هم کار کند

5) app pool را Recycle کنید.

--------------------------------------------------------------------
آزمایش:
   http://<IP>:<PORT>/            →  صفحه‌ی ورود
   http://<IP>:<PORT>/api/Health  →  {"status":"Healthy"}

--------------------------------------------------------------------
"@

if ($configPlaceholders.Count -gt 0) {
    $readme += @"
هشدار - فایل تنظیمات کامل نیست

  $ApiFolderName\appsettings.Production.json هنوز این جای‌نگهدارها را دارد:
    $($configPlaceholders -join '  ')

  تا وقتی پر نشوند برنامه بالا نمی‌آید یا به دیتابیس وصل نمی‌شود.
"@
} else {
    $readme += "فایل تنظیمات کامل است.`r`n"
}

$readmePath = Join-Path $OutputPath 'راهنما.txt'
Set-Content -Path $readmePath -Value $readme -Encoding UTF8
Write-Ok 'راهنما.txt'

# ---------------------------------------------------------------------------
Write-Step 'بازرسی نهایی'
# ---------------------------------------------------------------------------
$siteFiles = @(Get-ChildItem $siteOut -Recurse -File)
$apiFiles  = @(Get-ChildItem $apiOut  -Recurse -File)
Write-Ok "$SiteFolderName\ : $($siteFiles.Count) فایل"
Write-Ok "$ApiFolderName\ : $($apiFiles.Count) فایل"

# فایل‌های بک‌اند نباید به هر دلیلی در پوشه‌ی کلاینت باشند.
$leaked = @($siteFiles | Where-Object { $_.Name -eq 'HSEQ.API.dll' -or $_.Name -eq 'appsettings.Production.json' })
if ($leaked.Count -gt 0) { throw "فایل بک‌اند در پوشه‌ی کلاینت پیدا شد: $($leaked[0].FullName)" }
Write-Ok 'هیچ فایل بک‌اندی در پوشه‌ی کلاینت نیست'

Write-Host ''
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host 'آماده است. این پوشه را به سرور ببرید:' -ForegroundColor Green
Write-Host "  $OutputPath" -ForegroundColor White
Write-Host ''
Write-Host 'محتویاتش:' -ForegroundColor DarkGray
Write-Host "  $SiteFolderName\   →  D:\HouzoriApps\$SiteFolderName"
Write-Host "  $ApiFolderName\   →  D:\HouzoriApps\$ApiFolderName"
Write-Host "  راهنما.txt"
if ($configPlaceholders.Count -gt 0) {
    Write-Host ''
    Write-Host "توجه: appsettings.Production.json هنوز جای‌نگهدار دارد: $($configPlaceholders -join ' , ')" -ForegroundColor Yellow
}
Write-Host ''
