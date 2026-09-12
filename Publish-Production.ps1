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
foreach ($sub in 'Site', 'Database', 'Scripts', 'Config') {
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
# HSEQ.API.Tests قرارداد عمومی API را می‌آزماید - از جمله اینکه /api/Auth/login به
# اکشن می‌رسد و /api/api/Auth/login نمی‌رسد. اگر این‌ها نگذرند، بسته ساخته نمی‌شود:
# همان خرابی یک‌بار روی سرور رفت و ورودِ همه‌ی کاربران را قطع کرد.
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
# یک پوشه برای کل سایت: برنامه‌ی ASP.NET Core که فایل‌های کلاینت را هم از wwwroot
# سرو می‌کند. در IIS همین یک پوشه می‌شود مسیر فیزیکی سایت و دیگر Application
# جداگانه‌ای زیر /api لازم نیست.
$siteOut = Join-Path $ReleaseDir 'Site'
Invoke-Checked -Command 'dotnet' -Arguments @(
    'publish', $ApiProject,
    '-c', 'Release',
    '-o', $siteOut,
    '--nologo'
) -What 'dotnet publish'

# appsettings.Development.json نباید داخل بسته برود: تنظیمات محلی است و رشته‌ی اتصال
# ماشین توسعه‌دهنده را همراه خودش می‌برد.
Get-ChildItem -Path $siteOut -Filter 'appsettings.Development.json' -ErrorAction SilentlyContinue |
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
$packagedSettings = Join-Path $siteOut 'appsettings.json'
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

Write-Ok "بک‌اند در Site\ ($((Get-ChildItem $siteOut -Recurse -File).Count) فایل)"

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

    # آزمون‌های کلاینت روی اجراکننده‌ی داخلی Node اجرا می‌شوند (بدون هیچ وابستگی
    # اضافه). مهم‌ترینشان نشانی‌ای است که کلاینت برای ورود می‌سازد: باید دقیقاً
    # /api/Auth/login باشد.
    Invoke-Checked -Command 'npm' -Arguments @('test') -WorkingDirectory $ClientDir -What 'آزمون‌های کلاینت'

    # tsc -b داخل خود اسکریپت build هست، پس typecheck جدا لازم نیست.
    Invoke-Checked -Command 'npm' -Arguments @('run', 'build') -WorkingDirectory $ClientDir -What 'build کلاینت'

    $distDir = Join-Path $ClientDir 'dist'
    if (-not (Test-Path $distDir)) { throw 'build کلاینت خروجی dist نساخت.' }

    # کلاینت داخل wwwroot می‌نشیند، یعنی همان برنامه سرو‌اش می‌کند. wwwroot تنها
    # پوشه‌ای است که از راه وب خوانده می‌شود؛ DLLها و appsettings کنارش می‌مانند و
    # بیرون از دسترس وب.
    $webRoot = Join-Path $siteOut 'wwwroot'
    if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $webRoot -Force

    # web.config مخصوصِ چیدمان دو-سایتیِ قبلی است (بازنویسی SPA و سند پیش‌فرض). در
    # چیدمان تک‌سایتی همه‌ی آن کارها را خودِ برنامه انجام می‌دهد، و ماندنش داخل
    # wwwroot فقط یک فایل گیج‌کننده‌ی قابل دانلود است.
    Copy-Item -Path (Join-Path $distDir '*') -Destination $webRoot -Recurse -Force
    Remove-Item (Join-Path $webRoot 'web.config') -Force -ErrorAction SilentlyContinue

    Write-Ok "کلاینت در Site\wwwroot\ ($((Get-ChildItem $webRoot -Recurse -File).Count) فایل)"
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
$frontendDir = Join-Path $siteOut 'wwwroot'
if (Test-Path $frontendDir) {
    $hits = @(Get-ChildItem $frontendDir -Recurse -File -Include '*.js', '*.css', '*.html' |
        Select-String -Pattern 'localhost:\d+', '127\.0\.0\.1' -ErrorAction SilentlyContinue)
    foreach ($h in $hits) { $leakFindings += "نشانی محلی در Frontend: $($h.Filename):$($h.LineNumber)" }
}

# تنظیمات توسعه یا کلید لو رفته داخل بسته‌ی بک‌اند
$backendSettings = @(Get-ChildItem $siteOut -Filter 'appsettings*.json' -ErrorAction SilentlyContinue)
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
$components = @('Site')
if (-not $SkipFrontend) { $components += 'Site\wwwroot' }
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
    'Site\HSEQ.API.dll',
    'Site\web.config',
    'release.json',
    'SHA256SUMS.txt'
)
if (-not $SkipFrontend) { $expected += 'Site\wwwroot\index.html' }
if (-not $SkipDatabase) { $expected += 'Database\migrations.sql' }

$missing = @()
foreach ($rel in $expected) {
    if (-not (Test-Path (Join-Path $ReleaseDir $rel))) { $missing += $rel }
}
if ($missing.Count -gt 0) { throw "بسته ناقص است. موارد غایب: $($missing -join ', ')" }
Write-Ok 'همه‌ی اجزای مورد انتظار موجودند'

# و حالا همان اسکریپتی که روی سرور این بسته را مصرف می‌کند، خودش قضاوت کند.
#
# فهرست بالا چیزی است که *این* اسکریپت لازم می‌داند؛ چیزی که واقعاً اهمیت دارد این
# است که Deploy-Production.ps1 بتواند بسته را بخواند. آن دو یک‌بار از هم جدا افتادند
# و نتیجه‌اش بسته‌ای بود که تا روی سرور سالم به نظر می‌رسید.
$deployScriptPath = Join-Path $RepoRoot 'Deploy-Production.ps1'
if (Test-Path $deployScriptPath) {
    $validation = & pwsh -NoProfile -ExecutionPolicy Bypass -File $deployScriptPath `
        -PackagePath $ReleaseDir -ValidatePackageOnly 2>&1
    if ($LASTEXITCODE -ne 0) {
        $validation | ForEach-Object { Write-Warn $_ }
        throw "Deploy-Production.ps1 این بسته را نپذیرفت (کد خروجی $LASTEXITCODE)."
    }
    Write-Ok 'Deploy-Production.ps1 -ValidatePackageOnly سبز شد'
} else {
    Write-Warn 'Deploy-Production.ps1 پیدا نشد - اعتبارسنجی با مصرف‌کننده انجام نشد.'
}

# ---------------------------------------------------------------------------
Write-Step 'ساخت آرشیو انتقال'
# ---------------------------------------------------------------------------
# عمداً *پس از* بازرسی اسرار و اعتبارسنجی ساختار: چیزی بسته‌بندی می‌شود که قبلاً
# بررسی شده باشد، نه برعکس.
#
# آرشیو بیرون از پوشه‌ی بسته ساخته می‌شود تا SHA256SUMS.txt داخل آن معتبر بماند.
$archivePath = Join-Path $ArtifactRoot ("{0}.zip" -f $ReleaseId)
if (Test-Path $archivePath) { Remove-Item $archivePath -Force }

Compress-Archive -Path (Join-Path $ReleaseDir '*') -DestinationPath $archivePath -CompressionLevel Optimal
if (-not (Test-Path $archivePath)) { throw 'آرشیو ساخته نشد.' }

$archiveHash = (Get-FileHash $archivePath -Algorithm SHA256).Hash
$archiveSize = (Get-Item $archivePath).Length

# هش خودِ آرشیو کنارش نوشته می‌شود تا گیرنده بتواند سالم بودنِ انتقال را بسنجد.
"{0}  {1}" -f $archiveHash, (Split-Path $archivePath -Leaf) |
    Set-Content "$archivePath.sha256" -Encoding UTF8

# آرشیو باز و با فهرست هش‌های داخلی مقایسه می‌شود. بدون این، یک آرشیوِ ناقص تا
# روی سرور کشف نمی‌شد.
$verifyDir = Join-Path $env:TEMP ("hseq-verify-{0}" -f [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -Path $archivePath -DestinationPath $verifyDir -Force
    $expectedSums = Get-Content (Join-Path $ReleaseDir 'SHA256SUMS.txt')
    $mismatch = @()
    foreach ($line in $expectedSums) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s\s(.+)$') { continue }
        $hash = $Matches[1]; $rel = $Matches[2]
        $extracted = Join-Path $verifyDir $rel
        if (-not (Test-Path $extracted)) { $mismatch += "غایب: $rel"; continue }
        if ((Get-FileHash $extracted -Algorithm SHA256).Hash -ne $hash) { $mismatch += "هش متفاوت: $rel" }
    }
    if ($mismatch.Count -gt 0) {
        foreach ($m in $mismatch | Select-Object -First 10) { Write-Warn $m }
        throw "محتوای آرشیو با SHA256SUMS.txt جور در نمی‌آید ($($mismatch.Count) مورد)."
    }
    Write-Ok "$(($expectedSums | Where-Object { $_ -match '^[0-9A-Fa-f]{64}' }).Count) فایل داخل آرشیو تأیید شد"
}
finally {
    if (Test-Path $verifyDir) { Remove-Item $verifyDir -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Ok ("{0} ({1:N1} MB)" -f (Split-Path $archivePath -Leaf), ($archiveSize / 1MB))
Write-Ok "SHA-256: $archiveHash"

Write-Host ''
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host "وضعیت : $releaseState" -ForegroundColor $(if ($gitDirty) { 'Yellow' } else { 'Green' })
Write-Host "بسته  : $ReleaseDir" -ForegroundColor White
Write-Host "آرشیو : $archivePath" -ForegroundColor White
Write-Host "SHA256: $archiveHash" -ForegroundColor DarkGray
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host ''
