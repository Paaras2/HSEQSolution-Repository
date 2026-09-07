<#
.SYNOPSIS
    ساخت بسته‌ی انتشار سامانه مدیریت یکپارچه مدارک و مستندات (HSEQ).

.DESCRIPTION
    خروجی یک پوشه‌ی نسخه‌دار زیر artifacts\ است که شامل بک‌اند publish‌شده، فایل‌های
    استاتیک کلاینت، اسکریپت مهاجرت دیتابیس، اسکریپت استقرار و قالب تنظیمات است.

    این اسکریپت هیچ‌چیزی روی سرور تغییر نمی‌دهد؛ فقط بسته می‌سازد.

    فلسفه: fail fast. هر گام که شکست بخورد، اسکریپت با کد خروجی غیرصفر متوقف می‌شود
    و بسته‌ی ناقص باقی نمی‌ماند.

.PARAMETER SkipFrontend
    فقط بک‌اند بسته‌بندی شود. برای وقتی که فقط تغییر سمت سرور منتشر می‌شود.

.PARAMETER SkipDatabase
    اسکریپت مهاجرت تولید نشود.

.EXAMPLE
    .\Publish-Production.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipFrontend,
    [switch]$SkipDatabase
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$SystemName = 'HSEQ'
# ریشه از محل خودِ اسکریپت گرفته می‌شود، نه از دایرکتوری جاری - وگرنه اجرای اسکریپت
# از هر جای دیگری مسیرها را خراب می‌کرد.
$RepoRoot = $PSScriptRoot
$ReleaseId = "{0}_{1}" -f $SystemName, (Get-Date -Format 'yyyyMMdd-HHmmss')
$ArtifactRoot = Join-Path $RepoRoot 'artifacts'
$ReleaseDir = Join-Path $ArtifactRoot $ReleaseId

$ApiProject = Join-Path $RepoRoot 'HSEQ.API\HSEQ.API.csproj'
$DomainProject = Join-Path $RepoRoot 'HSEQ.Domain\HSEQ.Domain.csproj'
$ClientDir = Join-Path $RepoRoot 'hseq-client'

$script:StepNumber = 0

function Write-Step {
    param([string]$Message)
    $script:StepNumber++
    Write-Host ''
    Write-Host ("[{0,2}] {1}" -f $script:StepNumber, $Message) -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "     OK  $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "     !   $Message" -ForegroundColor Yellow
}

# هر فرمان بیرونی از این عبور می‌کند تا شکستِ خاموش نداشته باشیم.
function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$WorkingDirectory = $RepoRoot,
        [string]$What = 'command'
    )

    Push-Location $WorkingDirectory
    try {
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$What با کد خروجی $LASTEXITCODE شکست خورد: $Command $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ''
Write-Host "ساخت بسته‌ی انتشار $SystemName" -ForegroundColor White
Write-Host "شناسه نسخه: $ReleaseId"

# ---------------------------------------------------------------------------
Write-Step 'بررسی ابزارهای لازم'
# ---------------------------------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw 'dotnet SDK پیدا نشد. برای ساخت بسته لازم است (روی سرور عملیاتی لازم نیست).' }
$dotnetVersion = (& dotnet --version).Trim()
Write-Ok "dotnet SDK $dotnetVersion"

$nodeVersion = $null
$npmVersion = $null
if (-not $SkipFrontend) {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if (-not $node) { throw 'Node.js پیدا نشد. برای build کلاینت لازم است (روی سرور عملیاتی لازم نیست).' }
    $nodeVersion = (& node --version).Trim()
    $npmVersion = (& npm --version).Trim()
    Write-Ok "Node $nodeVersion / npm $npmVersion"
}

# ---------------------------------------------------------------------------
Write-Step 'خواندن وضعیت Git'
# ---------------------------------------------------------------------------
$gitBranch = 'unknown'
$gitCommit = 'unknown'
$gitDirty = $true
$dirtyPaths = @()

if (Get-Command git -ErrorAction SilentlyContinue) {
    Push-Location $RepoRoot
    try {
        $gitBranch = (& git rev-parse --abbrev-ref HEAD 2>$null)
        $gitCommit = (& git rev-parse HEAD 2>$null)
        $status = @(& git status --porcelain 2>$null)
        $gitDirty = $status.Count -gt 0
        # @() لازم است: روی درخت کاریِ تمیز خروجی خط لوله هیچ است، نه آرایه‌ی خالی،
        # و بعداً ‎.Count‎ روی ‎$null‎ زیر Set-StrictMode خطا می‌داد - یعنی اسکریپت
        # دقیقاً در حالتی می‌شکست که انتشار رسمی از آن ساخته می‌شود.
        $dirtyPaths = @($status | ForEach-Object { $_.Substring(3) })
    }
    finally { Pop-Location }
}

Write-Ok "شاخه $gitBranch / کامیت $($gitCommit.Substring(0, [Math]::Min(8, $gitCommit.Length)))"
if ($gitDirty) {
    Write-Warn "درخت کاری تمیز نیست ($($dirtyPaths.Count) مسیر تغییریافته)."
    Write-Warn 'این بسته به‌عنوان انتشار رسمی عملیاتی قابل استناد نیست: PRODUCTION_RELEASE_BLOCKED_DIRTY_WORKTREE'
}

# ---------------------------------------------------------------------------
Write-Step 'پاک‌سازی پوشه‌ی نسخه'
# ---------------------------------------------------------------------------
# فقط پوشه‌ی همین نسخه - نسخه‌های قبلی دست نمی‌خورند و هیچ wildcard‌ای اینجا نیست.
if (Test-Path $ReleaseDir) { Remove-Item $ReleaseDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path $ReleaseDir -Force
foreach ($sub in 'Backend', 'Frontend', 'Database', 'Scripts', 'Config') {
    $null = New-Item -ItemType Directory -Path (Join-Path $ReleaseDir $sub) -Force
}
Write-Ok $ReleaseDir

# ---------------------------------------------------------------------------
Write-Step 'بازیابی وابستگی‌های بک‌اند'
# ---------------------------------------------------------------------------
Invoke-Checked -Command 'dotnet' -Arguments @('restore', $ApiProject) -What 'dotnet restore'
Write-Ok 'restore انجام شد'

# ---------------------------------------------------------------------------
Write-Step 'اجرای تست‌های بک‌اند'
# ---------------------------------------------------------------------------
# در این مخزن هیچ پروژه‌ی تستی وجود ندارد. عمداً تست ساختگی تولید نمی‌شود؛ گام رد
# می‌شود و در گزارش به‌عنوان «اجرا نشد» ثبت می‌گردد.
$testProjects = @(Get-ChildItem -Path $RepoRoot -Filter '*.csproj' -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.BaseName -match '(?i)test' })
if ($testProjects.Count -eq 0) {
    Write-Warn 'پروژه‌ی تستی در مخزن نیست - این گام رد شد.'
} else {
    foreach ($proj in $testProjects) {
        Invoke-Checked -Command 'dotnet' -Arguments @('test', $proj.FullName, '-c', 'Release', '--nologo') -What 'dotnet test'
    }
    Write-Ok "$($testProjects.Count) پروژه‌ی تست پاس شد"
}

# ---------------------------------------------------------------------------
Write-Step 'publish بک‌اند (Release)'
# ---------------------------------------------------------------------------
$backendOut = Join-Path $ReleaseDir 'Backend'
Invoke-Checked -Command 'dotnet' -Arguments @(
    'publish', $ApiProject,
    '-c', 'Release',
    '-o', $backendOut,
    '--nologo'
) -What 'dotnet publish'

# appsettings.Development.json نباید داخل بسته برود: تنظیمات محلی است و رشته‌ی اتصال
# ماشین توسعه‌دهنده را همراه خودش می‌برد.
Get-ChildItem -Path $backendOut -Filter 'appsettings.Development.json' -ErrorAction SilentlyContinue |
    Remove-Item -Force

# appsettings.json پایه همیشه همراه برنامه منتشر می‌شود و مقادیر توسعه‌ی داخلش
# (رشته‌ی اتصالِ LocalDB و کلید امضای توسعه) در بسته‌ی عملیاتی جایی ندارند.
#
# برنامه بدون این‌ها هم بالا نمی‌آمد - ProductionConfigurationValidator هر دو را
# صریحاً رد می‌کند - ولی نبودنشان در بسته بهتر از بودنشان است: اپراتور موقع خواندن
# فایل به مقدارِ توسعه برنمی‌خورد و اشتباهی رویش تکیه نمی‌کند.
#
# جایگزینی روی متن خام انجام می‌شود نه با تبدیل JSON، چون این فایل کامنت دارد و
# رفت‌وبرگشت JSON کامنت‌ها و قالب‌بندی‌اش را از بین می‌برد.
$packagedSettings = Join-Path $backendOut 'appsettings.json'
if (Test-Path $packagedSettings) {
    $raw = Get-Content $packagedSettings -Raw
    $before = $raw
    # هر مقداری که نشانه‌ی توسعه دارد خالی می‌شود؛ کلیدش می‌ماند تا ساختار فایل
    # به‌هم نریزد و اعتبارسنجیِ راه‌اندازی «مقدار ندارد» را صریح گزارش کند.
    $raw = [regex]::Replace($raw, '"[^"]*\(localdb\)[^"]*"', '""')
    $raw = [regex]::Replace($raw, '"[^"]*DEVELOPMENT-ONLY[^"]*"', '""')
    if ($raw -ne $before) {
        Set-Content -Path $packagedSettings -Value $raw -Encoding UTF8 -NoNewline
        Write-Ok 'مقادیر توسعه از appsettings.json بسته پاک شد'
    }
}

Write-Ok "بک‌اند در Backend\ ($((Get-ChildItem $backendOut -Recurse -File).Count) فایل)"

# ---------------------------------------------------------------------------
Write-Step 'build کلاینت'
# ---------------------------------------------------------------------------
if ($SkipFrontend) {
    Write-Warn 'با سوئیچ -SkipFrontend رد شد.'
} else {
    # نصب قطعی از روی lockfile. npm install نه - آن می‌تواند نسخه‌ها را جابه‌جا کند.
    if (Test-Path (Join-Path $ClientDir 'package-lock.json')) {
        Invoke-Checked -Command 'npm' -Arguments @('ci') -WorkingDirectory $ClientDir -What 'npm ci'
    } else {
        Write-Warn 'package-lock.json نیست؛ npm install اجرا می‌شود (نصب غیرقطعی).'
        Invoke-Checked -Command 'npm' -Arguments @('install') -WorkingDirectory $ClientDir -What 'npm install'
    }

    Invoke-Checked -Command 'npm' -Arguments @('run', 'lint') -WorkingDirectory $ClientDir -What 'lint'
    # tsc -b داخل خود اسکریپت build هست، پس typecheck جدا لازم نیست.
    Invoke-Checked -Command 'npm' -Arguments @('run', 'build') -WorkingDirectory $ClientDir -What 'build کلاینت'

    $distDir = Join-Path $ClientDir 'dist'
    if (-not (Test-Path $distDir)) { throw 'build کلاینت خروجی dist نساخت.' }

    Copy-Item -Path (Join-Path $distDir '*') -Destination (Join-Path $ReleaseDir 'Frontend') -Recurse -Force
    Write-Ok "کلاینت در Frontend\ ($((Get-ChildItem (Join-Path $ReleaseDir 'Frontend') -Recurse -File).Count) فایل)"
}

# ---------------------------------------------------------------------------
Write-Step 'تولید اسکریپت مهاجرت دیتابیس'
# ---------------------------------------------------------------------------
$dbMethod = 'none'
if ($SkipDatabase) {
    Write-Warn 'با سوئیچ -SkipDatabase رد شد.'
} else {
    # اسکریپت idempotent: هر مهاجرت داخل یک IF NOT EXISTS روی __EFMigrationsHistory
    # می‌نشیند، پس اجرای دوباره‌اش روی دیتابیسِ به‌روز بی‌اثر است. برخلاف مهاجرت هنگام
    # بالا آمدن، این را می‌شود قبل از اجرا خواند و بازبینی کرد.
    $migrationScript = Join-Path $ReleaseDir 'Database\migrations.sql'
    Invoke-Checked -Command 'dotnet' -Arguments @(
        'ef', 'migrations', 'script',
        '--idempotent',
        '--project', $DomainProject,
        '--startup-project', $ApiProject,
        '--output', $migrationScript
    ) -What 'تولید اسکریپت مهاجرت'

    if (-not (Test-Path $migrationScript)) { throw 'اسکریپت مهاجرت تولید نشد.' }
    $dbMethod = 'idempotent-sql-script'
    $lineCount = (Get-Content $migrationScript | Measure-Object -Line).Lines
    Write-Ok "Database\migrations.sql ($lineCount خط، idempotent)"
}

# ---------------------------------------------------------------------------
Write-Step 'کپی اسکریپت استقرار و قالب تنظیمات'
# ---------------------------------------------------------------------------
$deployScript = Join-Path $RepoRoot 'Deploy-Production.ps1'
if (Test-Path $deployScript) {
    Copy-Item $deployScript -Destination (Join-Path $ReleaseDir 'Scripts') -Force
    Write-Ok 'Scripts\Deploy-Production.ps1'
} else {
    Write-Warn 'Deploy-Production.ps1 پیدا نشد.'
}

$configTemplate = Join-Path $RepoRoot 'deploy\appsettings.Production.template.json'
if (Test-Path $configTemplate) {
    Copy-Item $configTemplate -Destination (Join-Path $ReleaseDir 'Config') -Force
    Write-Ok 'Config\appsettings.Production.template.json'
}

$readme = Join-Path $RepoRoot 'README-DEPLOY.md'
if (Test-Path $readme) {
    Copy-Item $readme -Destination (Join-Path $ReleaseDir 'README-DEPLOY.md') -Force
    Write-Ok 'README-DEPLOY.md'
}

# ---------------------------------------------------------------------------
Write-Step 'بازرسی بسته از نظر نشانی محلی و اسرار'
# ---------------------------------------------------------------------------
$leakFindings = @()

# نشانی‌های توسعه داخل خروجی build کلاینت
$frontendDir = Join-Path $ReleaseDir 'Frontend'
if (Test-Path $frontendDir) {
    $hits = @(Get-ChildItem $frontendDir -Recurse -File -Include '*.js', '*.css', '*.html' |
        Select-String -Pattern 'localhost:\d+', '127\.0\.0\.1' -ErrorAction SilentlyContinue)
    foreach ($h in $hits) { $leakFindings += "نشانی محلی در Frontend: $($h.Filename):$($h.LineNumber)" }
}

# تنظیمات توسعه یا کلید لو رفته داخل بسته‌ی بک‌اند
$backendSettings = @(Get-ChildItem $backendOut -Filter 'appsettings*.json' -ErrorAction SilentlyContinue)
foreach ($f in $backendSettings) {
    if ($f.Name -like '*Development*') { $leakFindings += "فایل تنظیمات توسعه در بسته: $($f.Name)" }

    # محتوای فایل هم بررسی می‌شود، نه فقط نامش: مقدارِ توسعه ممکن است داخل
    # appsettings.json پایه مانده باشد. این بررسی مکملِ پاک‌سازی بالاست تا اگر روزی
    # آن گام خراب شد، بسته بی‌سروصدا با مقدار توسعه بیرون نرود.
    $content = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -match '\(localdb\)') { $leakFindings += "رشته‌ی اتصال LocalDB در $($f.Name)" }
    if ($content -match 'DEVELOPMENT-ONLY') { $leakFindings += "کلید توسعه در $($f.Name)" }
    # رمز داخل رشته‌ی اتصال - احراز هویت یکپارچه ویندوز رمز ندارد، پس دیدنش یعنی
    # اعتبارنامه‌ی واقعی داخل بسته نشسته است.
    if ($content -match '(?i)(password|pwd)\s*=\s*[^";\s]+') { $leakFindings += "رمز عبور داخل رشته‌ی اتصال در $($f.Name)" }
}

# فایل‌هایی که هرگز نباید در بسته باشند
foreach ($forbidden in '.env', '.env.local', '.env.development') {
    $found = @(Get-ChildItem $ReleaseDir -Recurse -File -Force -Filter $forbidden -ErrorAction SilentlyContinue)
    foreach ($f in $found) { $leakFindings += "فایل محیطی محلی در بسته: $($f.FullName.Substring($ReleaseDir.Length + 1))" }
}

if ($leakFindings.Count -gt 0) {
    foreach ($f in $leakFindings) { Write-Warn $f }
    throw "بازرسی بسته $($leakFindings.Count) مورد پیدا کرد. بسته منتشر نشد."
}
Write-Ok 'نشانی محلی یا فایل تنظیمات توسعه‌ای در بسته نیست'

# ---------------------------------------------------------------------------
Write-Step 'تولید release.json'
# ---------------------------------------------------------------------------
$components = @('Backend')
if (-not $SkipFrontend) { $components += 'Frontend' }
if (-not $SkipDatabase) { $components += 'Database' }
$components += 'Scripts'

$releaseState = if ($gitDirty) { 'PRODUCTION_RELEASE_BLOCKED_DIRTY_WORKTREE' } else { 'PACKAGE_READY' }

$release = [ordered]@{
    systemName       = $SystemName
    releaseId        = $ReleaseId
    buildTimestampUtc = (Get-Date).ToUniversalTime().ToString('o')
    buildTimestampLocal = (Get-Date).ToString('o')
    gitBranch        = $gitBranch
    gitCommit        = $gitCommit
    gitDirty         = $gitDirty
    dirtyPathCount   = $dirtyPaths.Count
    releaseState     = $releaseState
    dotnetSdk        = $dotnetVersion
    targetFramework  = 'net10.0'
    node             = $nodeVersion
    npm              = $npmVersion
    components       = $components
    databaseMethod   = $dbMethod
    checksumFile     = 'SHA256SUMS.txt'
}
$release | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $ReleaseDir 'release.json') -Encoding UTF8
Write-Ok 'release.json'

# ---------------------------------------------------------------------------
Write-Step 'تولید SHA256SUMS.txt'
# ---------------------------------------------------------------------------
$sumsPath = Join-Path $ReleaseDir 'SHA256SUMS.txt'
$files = Get-ChildItem $ReleaseDir -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' }
$lines = foreach ($f in $files) {
    $rel = $f.FullName.Substring($ReleaseDir.Length + 1)
    "{0}  {1}" -f (Get-FileHash $f.FullName -Algorithm SHA256).Hash, $rel
}
$lines | Set-Content $sumsPath -Encoding UTF8
Write-Ok "$($lines.Count) فایل هش شد"

# ---------------------------------------------------------------------------
Write-Step 'اعتبارسنجی ساختار بسته'
# ---------------------------------------------------------------------------
$expected = @(
    'Backend\HSEQ.API.dll',
    'Backend\web.config',
    'release.json',
    'SHA256SUMS.txt'
)
if (-not $SkipFrontend) { $expected += 'Frontend\index.html' }
if (-not $SkipDatabase) { $expected += 'Database\migrations.sql' }

$missing = @()
foreach ($rel in $expected) {
    if (-not (Test-Path (Join-Path $ReleaseDir $rel))) { $missing += $rel }
}
if ($missing.Count -gt 0) { throw "بسته ناقص است. موارد غایب: $($missing -join ', ')" }
Write-Ok 'همه‌ی اجزای مورد انتظار موجودند'

Write-Host ''
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host "وضعیت : $releaseState" -ForegroundColor $(if ($gitDirty) { 'Yellow' } else { 'Green' })
Write-Host "بسته  : $ReleaseDir" -ForegroundColor White
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host ''
