<#
.SYNOPSIS
    ساخت پوشه‌ی تحویل از یک بسته‌ی انتشارِ ساخته‌شده - بیرون از artifacts\.

.DESCRIPTION
    Publish-Production.ps1 بسته را می‌سازد و New-ServerLayout.ps1 آن را به شکلی که
    روی سرور روی دیسک می‌نشیند می‌چیند. این اسکریپت گام آخر است: هر دو خروجی را
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
    [string]$ConfigFile = ''
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

# چیدمان تحویل به تفکیک نقش: Frontend\ ، Backend\ ، Deployment\
#
# New-ServerLayout.ps1 دو پوشه را با نامِ *مقصدِ روی سرور* می‌سازد (HSEQTest و
# HSEQTest-api). آن نام‌ها همان‌جا هم می‌مانند، فقط یک لایه بالاتر زیر Frontend\ و
# Backend\ می‌نشینند - تا هم نقش هر پوشه از نامش پیدا باشد و هم نامی که باید روی
# دیسکِ سرور بنشیند گم نشود.
$layoutTemp = Join-Path $handoffDir '.layout'
# 6>$null و نه | Out-Null: آن اسکریپت با Write-Host می‌نویسد، و Write-Host به
# جریان information می‌رود نه به خط لوله - پس Out-Null چیزی را پنهان نمی‌کرد.
# پیام‌هایش هم اینجا گمراه‌کننده‌اند، چون مسیر موقتِ .layout را به‌عنوان «پوشه‌ای که
# باید به سرور ببرید» اعلام می‌کنند.
& (Join-Path $RepoRoot 'New-ServerLayout.ps1') `
    -PackagePath $PackagePath -ConfigFile $ConfigFile -OutputPath $layoutTemp 6>$null

# نتیجه بررسی می‌شود، نه کد خروجی: $LASTEXITCODE فقط برای برنامه‌های بومی مقدار
# می‌گیرد و پس از فراخوانی یک اسکریپت PowerShell دست‌نخورده می‌ماند - زیر
# Set-StrictMode خواندنش وقتی هرگز مقدار نگرفته باشد خودش خطا می‌دهد.
# شکستِ خودِ آن اسکریپت با throw و $ErrorActionPreference='Stop' به اینجا می‌رسد.
foreach ($expected in 'HSEQTest\index.html', 'HSEQTest-api\HSEQ.API.dll') {
    if (-not (Test-Path (Join-Path $layoutTemp $expected))) {
        throw "چیدمان سرور ناقص است - $expected ساخته نشد."
    }
}

$frontendDir = Join-Path $handoffDir 'Frontend'
$backendDir  = Join-Path $handoffDir 'Backend'
$null = New-Item -ItemType Directory -Path $frontendDir -Force
$null = New-Item -ItemType Directory -Path $backendDir -Force

Move-Item (Join-Path $layoutTemp 'HSEQTest')     $frontendDir -Force
Move-Item (Join-Path $layoutTemp 'HSEQTest-api') $backendDir  -Force
Move-Item (Join-Path $layoutTemp 'راهنما.txt')   (Join-Path $handoffDir 'COPY-TO-SERVER.txt') -Force
Remove-Item $layoutTemp -Recurse -Force

Write-Ok 'Frontend\HSEQTest\      →  D:\HouzoriApps\HSEQTest'
Write-Ok 'Backend\HSEQTest-api\   →  D:\HouzoriApps\HSEQTest-api'

# ---------------------------------------------------------------------------
Write-Step 'اسکریپت‌ها، دیتابیس و مستندات'
# ---------------------------------------------------------------------------
$deployDir = Join-Path $handoffDir 'Deployment'
$null = New-Item -ItemType Directory -Path $deployDir -Force

$scriptsDir = Join-Path $deployDir 'Scripts'
$null = New-Item -ItemType Directory -Path $scriptsDir -Force
foreach ($s in 'Deploy-Production.ps1', 'Diagnose-Deployment.ps1', 'New-ServerLayout.ps1') {
    Copy-Item (Join-Path $RepoRoot $s) $scriptsDir -Force
}
Write-Ok 'Deployment\Scripts\ (استقرار، عیب‌یابی، چیدمان)'

if (Test-Path (Join-Path $PackagePath 'Database\migrations.sql')) {
    $dbDir = Join-Path $deployDir 'Database'
    $null = New-Item -ItemType Directory -Path $dbDir -Force
    Copy-Item (Join-Path $PackagePath 'Database\migrations.sql') $dbDir -Force
    Write-Ok 'Deployment\Database\migrations.sql (idempotent)'
} else {
    Write-Warn 'بسته اسکریپت مهاجرت ندارد.'
}

Copy-Item (Join-Path $PackagePath 'README-DEPLOY.md') $deployDir -Force
Copy-Item $ConfigFile (Join-Path $deployDir 'appsettings.Production.template.json') -Force
Copy-Item (Join-Path $PackagePath 'release.json') $deployDir -Force
Write-Ok 'Deployment\ (راهنمای استقرار، قالب تنظیمات، release.json)'

# ---------------------------------------------------------------------------
Write-Step 'بازرسی: هیچ اسراری در تحویل نباشد'
# ---------------------------------------------------------------------------
# آخرین سد پیش از بسته‌بندی. یک کلید امضا یا رمزِ جامانده در پوشه‌ی تحویل، از
# لحظه‌ای که فایل دست کسی برسد لو رفته است.
$findings = @()
foreach ($f in Get-ChildItem $handoffDir -Recurse -File -Include '*.json', '*.config', '*.ps1', '*.txt', '*.md') {
    $content = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    $rel = $f.FullName.Substring($handoffDir.Length + 1)

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
Write-Ok 'نه رمزی، نه کلیدی، نه فایل تنظیمات توسعه‌ای'

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

  Frontend\HSEQTest\            →  D:\HouzoriApps\HSEQTest
  Backend\HSEQTest-api\         →  D:\HouzoriApps\HSEQTest-api
  Deployment\README-DEPLOY.md      راهنمای کامل استقرار
  Deployment\Scripts\              اسکریپت استقرار، عیب‌یابی و چیدمان
  Deployment\Database\             اسکریپت idempotent مهاجرت
  Deployment\appsettings.Production.template.json
                                   قالب تنظیمات (بدون هیچ مقدار واقعی)
  Deployment\release.json          فراداده‌ی نسخه
  COPY-TO-SERVER.txt               خلاصه‌ی گام‌های کپی روی سرور
  SHA256SUMS.txt                   هش همه‌ی فایل‌های بالا

----------------------------------------------------------------
قرارداد مسیر

مرورگر دقیقاً این نشانی را صدا می‌زند:

    /api/Auth/login

پیشوند /api یک مالک دارد: خودِ IIS، از راه Application با همان مسیر. هیچ
کنترلری آن را دوباره اعلام نمی‌کند، پس /api/api/... باید ۴۰۴ بدهد.

چیدمان لازم در IIS:

  Site        : HSEQTest         →  D:\HouzoriApps\HSEQTest
  Application : /api             →  D:\HouzoriApps\HSEQTest-api
  App pool    : HSEQTest (No Managed Code) - برای هر دو

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
