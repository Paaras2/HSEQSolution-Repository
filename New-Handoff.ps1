<#
.SYNOPSIS
    ساخت پوشه‌ی تحویل از یک بسته‌ی انتشارِ ساخته‌شده - بیرون از artifacts\.

.DESCRIPTION
    Publish-Production.ps1 بسته را می‌سازد. این اسکریپت گام آخر است: همان خروجی را
    کنار مستندات، اسکریپت‌ها و قالب تنظیمات در یک پوشه‌ی تاریخ‌دار جمع می‌کند و یک
    آرشیو انتقال از آن می‌سازد.

    چرا بیرون از artifacts\: آن پوشه محل خروجی build است و با هر انتشار پاک و
    دوباره ساخته می‌شود. چیزی که قرار است تحویل داده و بایگانی شود نباید آنجا
    بماند.

    این اسکریپت هیچ‌چیزی روی سرور یا دیتابیس تغییر نمی‌دهد و idempotent است:
    اجرای دوباره‌اش با همان بسته، همان محتوا را می‌سازد.

.PARAMETER PackagePath
    پوشه‌ی بسته (همان که release.json دارد). پیش‌فرض: تازه‌ترین بسته‌ی artifacts.

.PARAMETER OutputRoot
    ریشه‌ی محل ساخت. پیش‌فرض: پوشه‌ی کنارِ مخزن (‎..\HSEQ-Handoff‎).

.PARAMETER ConfigFile
    فایل تنظیماتی که در چیدمان سرور کنار HSEQ.API.dll می‌نشیند.

    پیش‌فرض عمداً **قالب** است، نه appsettings.Production.json محلی: آن فایل روی
    ماشین توسعه‌دهنده مقادیر واقعی (از جمله کلید امضای JWT) دارد و بسته‌ی تحویلی
    جای چنین چیزی نیست. قالب با جای‌نگهدار می‌رود و روی سرور پر می‌شود.

.EXAMPLE
    .\New-Handoff.ps1
#>
[CmdletBinding()]
param(
    [string]$PackagePath = '',
    [string]$OutputRoot = '',
    [string]$ConfigFile = '',

    # اجازه‌ی قرار گرفتن تنظیماتِ پرشده - با رمز و کلید - داخل خودِ بسته.
    #
    # پیش‌فرض این نیست، و دلیلش روشن است: آرشیو دست‌به‌دست می‌شود و هر رونوشتش آن
    # دو مقدار را با خود می‌برد. ولی جدا نگه داشتنِ فایل هم خطای خودش را دارد -
    # یک‌بار بسته به سرور رفت و فایل تنظیمات همراهش نبود، پس قالبِ پرنشده سر جایش
    # ماند و برنامه بالا نیامد. هر دو حالت خطر دارد؛ این سوئیچ انتخاب را صریح
    # می‌کند به‌جای اینکه یکی را بی‌صدا تحمیل کند.
    [switch]$IncludeSecrets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = $PSScriptRoot
$step = 0
function Write-Step { param([string]$m) $script:step++; Write-Host ''; Write-Host ("[{0}] {1}" -f $script:step, $m) -ForegroundColor Cyan }
function Write-Ok   { param([string]$m) Write-Host "    OK  $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "    !   $m" -ForegroundColor Yellow }

Write-Host ''
Write-Host 'ساخت پوشه‌ی تحویل' -ForegroundColor White

# ---------------------------------------------------------------------------
Write-Step 'یافتن بسته'
# ---------------------------------------------------------------------------
if (-not $PackagePath) {
    $artifacts = Join-Path $RepoRoot 'artifacts'
    if (-not (Test-Path $artifacts)) { throw 'پوشه‌ی artifacts نیست. اول Publish-Production.ps1 را اجرا کنید.' }
    $candidate = Get-ChildItem $artifacts -Directory |
                 Where-Object { Test-Path (Join-Path $_.FullName 'release.json') } |
                 Sort-Object Name | Select-Object -Last 1
    if (-not $candidate) { throw 'بسته‌ای در artifacts نیست. اول Publish-Production.ps1 را اجرا کنید.' }
    $PackagePath = $candidate.FullName
}
$PackagePath = (Resolve-Path $PackagePath).Path
if (-not (Test-Path (Join-Path $PackagePath 'release.json'))) {
    throw "این پوشه بسته نیست (release.json ندارد): $PackagePath"
}

$release = Get-Content (Join-Path $PackagePath 'release.json') -Raw | ConvertFrom-Json
Write-Ok "$($release.releaseId) / کامیت $($release.gitCommit.Substring(0,8)) / وضعیت $($release.releaseState)"

# بسته‌ای که از درخت کاریِ کثیف ساخته شده به هیچ کامیتی قابل استناد نیست، پس به
# هیچ کسی هم نباید تحویل داده شود.
if ($release.gitDirty) {
    throw @"
این بسته از درخت کاری تمیز ساخته نشده و قابل تحویل نیست.
تغییرات را commit کنید و Publish-Production.ps1 را دوباره اجرا کنید.
"@
}

# ---------------------------------------------------------------------------
Write-Step 'بررسی صحت بسته با SHA256SUMS.txt'
# ---------------------------------------------------------------------------
# پیش از بسته‌بندی برای تحویل، سالم بودنِ خودِ بسته سنجیده می‌شود. بدون این، یک
# فایلِ خراب یا نیمه‌کپی‌شده تا روی سرور کشف نمی‌شد.
$sumsPath = Join-Path $PackagePath 'SHA256SUMS.txt'
if (-not (Test-Path $sumsPath)) { throw 'بسته SHA256SUMS.txt ندارد.' }

$checked = 0
$bad = @()
foreach ($line in Get-Content $sumsPath) {
    if ($line -notmatch '^([0-9A-Fa-f]{64})\s\s(.+)$') { continue }
    $hash = $Matches[1]; $rel = $Matches[2]
    $file = Join-Path $PackagePath $rel
    if (-not (Test-Path $file)) { $bad += "غایب: $rel"; continue }
    if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $hash) { $bad += "هش متفاوت: $rel" }
    $checked++
}
if ($bad.Count -gt 0) {
    foreach ($b in $bad | Select-Object -First 10) { Write-Warn $b }
    throw "بسته سالم نیست ($($bad.Count) مورد)."
}
Write-Ok "$checked فایل تأیید شد"

# ---------------------------------------------------------------------------
Write-Step 'چیدمان آماده‌ی سرور'
# ---------------------------------------------------------------------------
if (-not $ConfigFile) {
    $ConfigFile = Join-Path $PackagePath 'Config\appsettings.Production.template.json'
}
if (-not (Test-Path $ConfigFile)) { throw "فایل تنظیمات پیدا نشد: $ConfigFile" }

if (-not $OutputRoot) { $OutputRoot = Join-Path (Split-Path -Parent $RepoRoot) 'HSEQ-Handoff' }
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$handoffName = "HSEQ_IIS_Release_$stamp"
$handoffDir = Join-Path $OutputRoot $handoffName

if (Test-Path $handoffDir) { Remove-Item $handoffDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path $handoffDir -Force

# ساختار بسته - همان شکلی که Deploy-Production.ps1 مصرف می‌کند.
#
# یک پوشه برای کل سایت: برنامه‌ی ASP.NET Core که فایل‌های کلاینت را هم از wwwroot
# سرو می‌کند. در IIS فقط یک سایت لازم است و Application جداگانه‌ای زیر /api نیست -
# پس پیشوند /api دیگر بین IIS و برنامه دست‌به‌دست نمی‌شود و کل دسته خرابی‌هایی که
# از همان تقسیم می‌آمد اصلاً موضوعیت ندارد.
$siteDir = Join-Path $handoffDir 'Site'
$null = New-Item -ItemType Directory -Path $siteDir -Force
Copy-Item -Path (Join-Path $PackagePath 'Site\*') -Destination $siteDir -Recurse -Force

# فراداده‌ی نسخه در ریشه - جایی که Deploy-Production.ps1 دنبالش می‌گردد.
Copy-Item (Join-Path $PackagePath 'release.json') $handoffDir -Force

# قالب تنظیمات کنار HSEQ.API.dll می‌نشیند تا اپراتور دنبالش نگردد. جای‌نگهدار دارد
# و برنامه تا پر نشدنش عمداً بالا نمی‌آید.
Copy-Item $ConfigFile (Join-Path $siteDir 'appsettings.Production.json') -Force

if (Test-Path (Join-Path $PackagePath 'Database\migrations.sql')) {
    $dbDir = Join-Path $handoffDir 'Database'
    $null = New-Item -ItemType Directory -Path $dbDir -Force
    Copy-Item (Join-Path $PackagePath 'Database\migrations.sql') $dbDir -Force
}

# نشانه‌ی نسخه، تا روی سرور معلوم باشد کدام انتشار نشسته است.
$stamp = @"
$($release.releaseId)
کامیت : $($release.gitCommit.Substring(0,8))
شاخه  : $($release.gitBranch)
ساخت  : $($release.buildTimestampLocal)
"@
Set-Content -Path (Join-Path $siteDir 'RELEASE.txt') -Value $stamp -Encoding UTF8

foreach ($expected in 'Site\HSEQ.API.dll', 'Site\wwwroot\index.html', 'release.json') {
    if (-not (Test-Path (Join-Path $handoffDir $expected))) {
        throw "بسته ناقص است - $expected ساخته نشد."
    }
}
Write-Ok 'Site\           (برنامه: HSEQ.API.dll، web.config، تنظیمات)'
Write-Ok 'Site\wwwroot\   (کلاینت، که همین برنامه سرو می‌کند)'
Write-Ok 'release.json در ریشه‌ی بسته'

# ---------------------------------------------------------------------------
Write-Step 'اسکریپت‌ها و مستندات'
# ---------------------------------------------------------------------------
$deployDir = Join-Path $handoffDir 'Deployment'
$null = New-Item -ItemType Directory -Path $deployDir -Force

$scriptsDir = Join-Path $deployDir 'Scripts'
$null = New-Item -ItemType Directory -Path $scriptsDir -Force
foreach ($s in 'Deploy-Production.ps1', 'Diagnose-Deployment.ps1', 'Show-StartupFailure.ps1', 'Test-UmLogin.ps1') {
    Copy-Item (Join-Path $RepoRoot $s) $scriptsDir -Force
}
Write-Ok 'Deployment\Scripts\ (استقرار، عیب‌یابی، شکست راه‌اندازی، سنجش ورود)'

Copy-Item (Join-Path $PackagePath 'README-DEPLOY.md') $deployDir -Force
# همیشه از قالبِ داخل بسته، نه از $ConfigFile: وقتی $ConfigFile مقادیر واقعی
# دارد، کپی کردنش زیر نام «template» یک نسخه‌ی دومِ رمز می‌سازد - جایی که
# هیچ‌کس دنبال رمز نمی‌گردد.
Copy-Item (Join-Path $PackagePath 'Config\appsettings.Production.template.json') `
          (Join-Path $deployDir 'appsettings.Production.template.json') -Force
Write-Ok 'Deployment\ (راهنمای استقرار، قالب تنظیمات)'

# ---------------------------------------------------------------------------
Write-Step 'بازرسی: هیچ اسراری در تحویل نباشد'
# ---------------------------------------------------------------------------
# آخرین سد پیش از بسته‌بندی. یک کلید امضا یا رمزِ جامانده در پوشه‌ی تحویل، از
# لحظه‌ای که فایل دست کسی برسد لو رفته است.
$findings = @()
$secretsCarried = $false
foreach ($f in Get-ChildItem $handoffDir -Recurse -File -Include '*.json', '*.config', '*.ps1', '*.txt', '*.md') {
    $content = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    $rel = $f.FullName.Substring($handoffDir.Length + 1)

    # تنظیماتِ عملیاتی، فقط وقتی که صریحاً خواسته شده باشد.
    #
    # استثنا عمداً به *یک مسیر مشخص* محدود است، نه به نوع فایل: رمزی که جای دیگری
    # جا مانده باشد - در یک اسکریپت، یک یادداشت، یا نسخه‌ی دومی از تنظیمات - هنوز
    # همین‌جا گیر می‌افتد.
    if ($IncludeSecrets -and $rel -eq (Join-Path 'Site' 'appsettings.Production.json')) {
        $secretsCarried = $true
        continue
    }

    # رمز داخل رشته‌ی اتصال - جای‌نگهدارِ <...> استثناست.
    if ($content -match '(?i)(password|pwd)\s*=\s*(?!<)[^";\s]+') { $findings += "رمز عبور در $rel" }
    if ($content -match '\(localdb\)')       { $findings += "رشته‌ی اتصال LocalDB در $rel" }
    if ($content -match 'DEVELOPMENT-ONLY')  { $findings += "کلید توسعه در $rel" }

    # کلید JWT پرشده: مقدار base64 بلند به‌جای جای‌نگهدار.
    if ($rel -like '*appsettings*' -and $content -match '"Key"\s*:\s*"(?!<)[A-Za-z0-9+/=]{32,}"') {
        $findings += "کلید JWT واقعی در $rel"
    }
}
foreach ($forbidden in '.env', '.env.local', '.env.development', 'appsettings.Development.json') {
    foreach ($f in Get-ChildItem $handoffDir -Recurse -File -Force -Filter $forbidden -ErrorAction SilentlyContinue) {
        $findings += "فایل محیط توسعه در تحویل: $($f.FullName.Substring($handoffDir.Length + 1))"
    }
}
if ($findings.Count -gt 0) {
    foreach ($f in $findings) { Write-Warn $f }
    throw "بازرسی $($findings.Count) مورد پیدا کرد. تحویل ساخته نشد."
}
if ($secretsCarried) {
    Write-Warn 'این بسته تنظیماتِ پرشده را با خود دارد: رمز دیتابیس و کلید امضای JWT.'
    Write-Warn 'ایمیلش نکنید، در پوشه‌ی اشتراکی نگذارید، و پس از استقرار پاکش کنید.'
} else {
    Write-Ok 'نه رمزی، نه کلیدی، نه فایل تنظیمات توسعه‌ای'
}

# ---------------------------------------------------------------------------
Write-Step 'یادداشت انتشار'
# ---------------------------------------------------------------------------
$notes = @"
$handoffName
================================================================

نسخه        : $($release.releaseId)
کامیت       : $($release.gitCommit)
شاخه        : $($release.gitBranch)
ساخت بسته   : $($release.buildTimestampLocal)
ساخت تحویل  : $(Get-Date -Format 'o')
.NET SDK    : $($release.dotnetSdk)  /  هدف: $($release.targetFramework)
Node / npm  : $($release.node) / $($release.npm)

----------------------------------------------------------------
محتویات

  release.json                     فراداده‌ی نسخه (ریشه - اسکریپت استقرار اینجا می‌گردد)
  SHA256SUMS.txt                   هش همه‌ی فایل‌ها، نسبت به همین ریشه
  Site\                            محتویاتش →  D:\HouzoriApps\HSEQTest
  Site\wwwroot\                    کلاینت، که همین برنامه سرو می‌کند
  Database\migrations.sql          اسکریپت idempotent مهاجرت
  Deployment\README-DEPLOY.md      راهنمای کامل استقرار
  Deployment\Scripts\              استقرار، عیب‌یابی، و Show-StartupFailure برای خطای 500.30
  Deployment\appsettings.Production.template.json
                                   قالب تنظیمات (بدون هیچ مقدار واقعی)
  COPY-TO-SERVER.txt               خلاصه‌ی گام‌های کپی روی سرور

چیدمان تک‌سایتی: فقط *یک* سایت در IIS، بدون Application جداگانه زیر /api. همان
برنامه هم API را سرو می‌کند و هم فایل‌های کلاینت را از wwwroot.

*محتویات* Site\ را کپی کنید، نه خودِ پوشه را؛ وگرنه مقصد یک لایه اضافه پیدا
می‌کند و IIS خطای 403.14 می‌دهد.

----------------------------------------------------------------
استقرار خودکار (توصیه‌شده)

همین پوشه را مستقیماً به اسکریپت بدهید - جابه‌جایی دستی لازم نیست:

  .\Deployment\Scripts\Deploy-Production.ps1 -PackagePath "<مسیر همین پوشه>" -DryRun

اول با -DryRun، و بعد همان فرمان بدون آن. اسکریپت خودش پشتیبان می‌گیرد، فایل‌ها را
می‌نشاند، Application با مسیر /api را می‌سازد و آزمون‌های دود را اجرا می‌کند.

فقط بررسی سلامت خودِ بسته، بدون هیچ تغییری و بدون نیاز به دسترسی مدیر:

  .\Deployment\Scripts\Deploy-Production.ps1 -PackagePath "<مسیر همین پوشه>" -ValidatePackageOnly

----------------------------------------------------------------
قرارداد مسیر

مرورگر دقیقاً این نشانی را صدا می‌زند:

    /api/Auth/login

پیشوند /api یک مالک دارد: خودِ IIS، از راه Application با همان مسیر. هیچ
کنترلری آن را دوباره اعلام نمی‌کند، پس /api/api/... باید ۴۰۴ بدهد.

چیدمان لازم در IIS:

  Site     : HSEQTest   →  D:\HouzoriApps\HSEQTest
  App pool : HSEQTest (No Managed Code)

  هیچ Applicationی زیر /api ثبت نمی‌شود. اگر از استقرار قبلی مانده باشد، اسکریپت
  استقرار برش می‌دارد - وگرنه IIS درخواست‌های /api/... را به آن قدیمی می‌داد.

----------------------------------------------------------------
پیش از استقرار

  * appsettings.Production.json کنار HSEQ.API.dll را از روی قالب پر کنید،
    یا مقادیر را به‌صورت متغیر محیطی روی Application Pool بگذارید:
        ConnectionStrings__DefaultConnection
        Jwt__Key
  * کلید JWT تازه بسازید. کلیدِ داخل مخزن سوخته است و برنامه ردش می‌کند.
  * از دیتابیس پشتیبان بگیرید. مهاجرت گام جداگانه‌ای است (-ApplySchema).

----------------------------------------------------------------
پس از استقرار - بررسی سریع

    <BASE>/api/Health           →  200  {"status":"Healthy"}
    <BASE>/api/api/Health       →  404  (اگر پاسخ داد، بسته قدیمی است)
    POST <BASE>/api/Auth/login  →  400  (نه 404، نه 500)

جزئیات کامل و رویه‌ی بازگشت به نسخه‌ی قبل در Deployment\README-DEPLOY.md
"@
Set-Content -Path (Join-Path $handoffDir 'RELEASE-NOTES.txt') -Value $notes -Encoding UTF8
Write-Ok 'RELEASE-NOTES.txt'

# متن بخش «تنظیمات» بسته به اینکه فایل پرشده داخل بسته باشد یا نه فرق می‌کند.
# گفتنِ «فایل را جداگانه بیاورید» وقتی فایل همان‌جاست، خودش گمراه‌کننده است.
$configNote = if ($secretsCarried) {
@'
appsettings.Production.json کنار HSEQ.API.dll از قبل با مقادیر واقعی این سرور
پر شده است. کاری لازم نیست.

⚠ یعنی همین بسته رمز دیتابیس و کلید امضای JWT را با خود دارد. ایمیلش نکنید،
  در پوشه‌ی اشتراکی نگذارید، و پس از استقرار نسخه‌های اضافه‌اش را پاک کنید.

نکته: این فایل با استقرارهای بعدی حفظ می‌شود - اسکریپت پیش از کپی کنارش
می‌گذارد و بعد برش می‌گرداند.
'@
} else {
@'
appsettings.Production.json کنار HSEQ.API.dll جای‌نگهدار دارد و باید پر شود،
یا مقادیر را روی Application Pool به‌صورت متغیر محیطی بگذارید:

    ConnectionStrings__DefaultConnection
    Jwt__Key

تا پر نشوند برنامه عمداً بالا نمی‌آید و در لاگ می‌گوید کدام کلید مانده.
'@
}

# برگه‌ی کوتاهِ کپی، برای کسی که سراغ اسکریپت نمی‌رود و دستی کپی می‌کند.
$copyGuide = @"
$handoffName
================================================================

دو راه دارید. راه اول امن‌تر است چون خودش پشتیبان می‌گیرد و در پایان آزمون می‌کند.


راه ۱ - با اسکریپت (توصیه‌شده)
----------------------------------------------------------------
در PowerShell با دسترسی Administrator، همین پوشه را بدهید:

  .\Deployment\Scripts\Deploy-Production.ps1 -PackagePath "<مسیر همین پوشه>" -DryRun

اگر همه‌ی بررسی‌ها سبز شد، همان فرمان را بدون -DryRun اجرا کنید.
هیچ جابه‌جایی دستی لازم نیست.


راه ۲ - کپی دستی
----------------------------------------------------------------
*محتویات* هر پوشه را کپی کنید، نه خودِ پوشه را:

  محتویات Site\   →  D:\HouzoriApps\HSEQTest

درست:  D:\HouzoriApps\HSEQTest\HSEQ.API.dll
       D:\HouzoriApps\HSEQTest\wwwroot\index.html
غلط :  D:\HouzoriApps\HSEQTest\Site\HSEQ.API.dll   ← خطای 403.14

سپس:

  1) دسترسی خواندن برای IIS:
     icacls "D:\HouzoriApps\HSEQTest" /grant "IIS_IUSRS:(OI)(CI)RX" /T

  2) مسیر مدارک و لاگ، با دسترسی نوشتن برای app pool:
     mkdir C:\ApplicationData\HSEQ\Documents
     mkdir C:\ApplicationData\HSEQ\logs
     icacls "C:\ApplicationData\HSEQ\Documents" /grant "IIS AppPool\HSEQTest:(OI)(CI)M"
     icacls "C:\ApplicationData\HSEQ\logs"      /grant "IIS AppPool\HSEQTest:(OI)(CI)M"

  3) در IIS Manager:
     - مسیر فیزیکی سایت HSEQTest  →  D:\HouzoriApps\HSEQTest
     - app pool → Basic Settings → .NET CLR Version = No Managed Code
     - اگر Applicationی با نام api زیر سایت هست، حذفش کنید. در این چیدمان
       لازم نیست و درخواست‌های /api/... را از سایت می‌دزدد.

  4) app pool را Recycle کنید.


تنظیمات
----------------------------------------------------------------
$configNote


آزمون پس از استقرار
----------------------------------------------------------------
    <BASE>/api/Health           →  200
    <BASE>/api/api/Health       →  404   (اگر جواب داد، بسته‌ی قدیمی مستقر شده)
    POST <BASE>/api/Auth/login  →  400   (نه ۴۰۴)


اول از همه: ورود را بسنجید
----------------------------------------------------------------
صفحه‌ی ورود اعتبار را از سامانه‌ی مدیریت کاربران می‌پرسد - بسته به
UserManagement:Source، از سرویس checkCredential آن (Api) یا مستقیم از دیتابیسش
(Database). پیش از آنکه سراغ مرورگر بروید، همین‌جا بسنجیدش - از مرورگر هر خرابی
فقط یک پیام خطاست و نمی‌شود فهمید مقصر IIS است یا سامانه‌ی کاربران یا دسترسی:

  .\Deployment\Scripts\Test-UmLogin.ps1
  .\Deployment\Scripts\Test-UmLogin.ps1 -Username <کد پرسنلی خودتان>

رمز روی صفحه echo نمی‌شود و هیچ‌جا ذخیره نمی‌شود.

در حالت Api نشانی باید https و با نام میزبان باشد: https://usermanagement.odcc.ir/api/
نشانیِ http روی پورت ۸۰ صفحه‌ی خطای HTML می‌دهد و پورت ۸۰۳۰ به IP هدایت می‌کند؛
هر دو ورود را شکست می‌دهند.

در حالت Database محتمل‌ترین خرابی این است که لاگین SQL روی HSEQDb مجاز باشد ولی روی
UserManagement نگاشت نداشته باشد. اگر پیامش همین را گفت، روی SQL Server:

  USE [UserManagement];
  CREATE USER [DBHSEQ] FOR LOGIN [DBHSEQ];
  ALTER ROLE db_datareader ADD MEMBER [DBHSEQ];

اگر پیام گفت قالبِ رمز شناخته نشد، در appsettings.Production.json موقتاً
"UserManagement": { "Source": "Api" } بگذارید تا از سرویس HTTP وارد شوید.
"@
Set-Content -Path (Join-Path $handoffDir 'COPY-TO-SERVER.txt') -Value $copyGuide -Encoding UTF8
Write-Ok 'COPY-TO-SERVER.txt'

# ---------------------------------------------------------------------------
Write-Step 'SHA256SUMS.txt'
# ---------------------------------------------------------------------------
$handoffSums = Join-Path $handoffDir 'SHA256SUMS.txt'
$files = Get-ChildItem $handoffDir -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' }
$lines = foreach ($f in $files) {
    "{0}  {1}" -f (Get-FileHash $f.FullName -Algorithm SHA256).Hash, $f.FullName.Substring($handoffDir.Length + 1)
}
$lines | Set-Content $handoffSums -Encoding UTF8
Write-Ok "$($lines.Count) فایل هش شد"

# ---------------------------------------------------------------------------
Write-Step 'اعتبارسنجی بسته با خودِ اسکریپت استقرار'
# ---------------------------------------------------------------------------
# اینجا عمداً فهرست جداگانه‌ای از «چه چیزهایی باید باشد» نوشته نشده. خودِ
# Deploy-Production.ps1 - همان اسکریپتی که روی سرور این بسته را مصرف می‌کند - روی
# خروجی اجرا می‌شود.
#
# چرا: پیش از این، شکلِ بسته و انتظارِ اسکریپت استقرار دو جای مستقل تعریف شده بودند
# و بی‌سروصدا از هم جدا افتادند. نتیجه بسته‌ای بود که ساخته و هش و آرشیو می‌شد، و
# تازه روی سرور معلوم می‌شد اسکریپت استقرار نمی‌تواند بخواندش. با اجرای خودِ
# مصرف‌کننده، چنین جدایی‌ای دیگر ممکن نیست: اگر انتظارش عوض شود و بسته همراهش نیاید،
# همین‌جا شکست می‌خورد.
#
# نسخه‌ای که *داخل بسته* است اجرا می‌شود، نه نسخه‌ی مخزن - چون همان نسخه به سرور
# می‌رود.
$packagedDeployScript = Join-Path $scriptsDir 'Deploy-Production.ps1'
if (-not (Test-Path $packagedDeployScript)) {
    throw 'Deploy-Production.ps1 در بسته نیست - بدون آن بسته قابل استقرار نیست.'
}

# در یک pwsh جدا، تا exit خودش این اسکریپت را نکُشد و کد خروجی هم واقعی باشد.
$validation = & pwsh -NoProfile -ExecutionPolicy Bypass -File $packagedDeployScript `
    -PackagePath $handoffDir -ValidatePackageOnly 2>&1
$validationExit = $LASTEXITCODE

if ($validationExit -ne 0) {
    Write-Host ''
    $validation | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    throw "Deploy-Production.ps1 این بسته را نپذیرفت (کد خروجی $validationExit). بسته تحویل‌دادنی نیست."
}
Write-Ok 'Deploy-Production.ps1 -ValidatePackageOnly روی این بسته سبز شد'

# چیزهایی که فقط به تحویل مربوط‌اند و اسکریپت استقرار کاری با آن‌ها ندارد.
$handoffExtras = @(
    'Deployment\README-DEPLOY.md',
    'Deployment\Scripts\Deploy-Production.ps1',
    'Deployment\Scripts\Diagnose-Deployment.ps1',
    'Deployment\Scripts\Show-StartupFailure.ps1',
    'Deployment\Scripts\Test-UmLogin.ps1',
    'Deployment\appsettings.Production.template.json',
    'Database\migrations.sql',
    'COPY-TO-SERVER.txt',
    'RELEASE-NOTES.txt'
)
$missingExtras = @($handoffExtras | Where-Object { -not (Test-Path (Join-Path $handoffDir $_)) })
if ($missingExtras.Count -gt 0) {
    throw "بسته‌ی تحویل ناقص است: $($missingExtras -join ' , ')"
}
Write-Ok "$($handoffExtras.Count) قلم مستندات و اسکریپت سر جایشان‌اند"

# ---------------------------------------------------------------------------
Write-Step 'آرشیو انتقال'
# ---------------------------------------------------------------------------
# فقط پس از اینکه محتویات باز و بازرسی شده‌اند.
$archivePath = Join-Path $OutputRoot "$handoffName.zip"
if (Test-Path $archivePath) { Remove-Item $archivePath -Force }
Compress-Archive -Path (Join-Path $handoffDir '*') -DestinationPath $archivePath -CompressionLevel Optimal

$archiveHash = (Get-FileHash $archivePath -Algorithm SHA256).Hash
"{0}  {1}" -f $archiveHash, (Split-Path $archivePath -Leaf) |
    Set-Content "$archivePath.sha256" -Encoding UTF8

# باز کردن دوباره و مقایسه با فهرست هش‌ها؛ یک آرشیو ناقص همین‌جا گیر می‌افتد.
$verifyDir = Join-Path $env:TEMP ("hseq-handoff-verify-{0}" -f [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -Path $archivePath -DestinationPath $verifyDir -Force
    $mismatch = @()
    foreach ($line in Get-Content $handoffSums) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s\s(.+)$') { continue }
        $extracted = Join-Path $verifyDir $Matches[2]
        if (-not (Test-Path $extracted)) { $mismatch += "غایب: $($Matches[2])"; continue }
        if ((Get-FileHash $extracted -Algorithm SHA256).Hash -ne $Matches[1]) { $mismatch += "هش متفاوت: $($Matches[2])" }
    }
    if ($mismatch.Count -gt 0) {
        foreach ($m in $mismatch | Select-Object -First 10) { Write-Warn $m }
        throw "محتوای آرشیو با SHA256SUMS.txt جور در نمی‌آید ($($mismatch.Count) مورد)."
    }
    Write-Ok 'محتوای آرشیو با فهرست هش‌ها جور است'
}
finally {
    if (Test-Path $verifyDir) { Remove-Item $verifyDir -Recurse -Force -ErrorAction SilentlyContinue }
}

$sizeMb = (Get-Item $archivePath).Length / 1MB
Write-Host ''
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host 'تحویل آماده است.' -ForegroundColor Green
Write-Host "پوشه  : $handoffDir" -ForegroundColor White
Write-Host ("آرشیو : {0}  ({1:N1} MB)" -f $archivePath, $sizeMb) -ForegroundColor White
Write-Host "SHA256: $archiveHash" -ForegroundColor DarkGray
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host ''
