# اجرای محیط توسعه‌ی محلی: API و کلاینت را با هم بالا می‌آورد.
#
# نسخه‌ی قبلی این فایل در خط آخر خودش را صدا می‌زد (‎.\start-dev.ps1‎) و برای همین با
# «call depth overflow» متوقف می‌شد و هیچ‌وقت چیزی اجرا نمی‌کرد.

# ریشه‌ی پروژه = پوشه‌ی خودِ اسکریپت. مسیر ثابت نوشته نمی‌شود تا روی هر ماشین/کلون کار کند.
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root

$apiProject = Join-Path $root 'HSEQ.API\HSEQ.API.csproj'
$clientDir  = Join-Path $root 'hseq-client'

# بررسی پیش‌نیازها پیش از باز کردن پنجره‌ها، تا خطا در پنجره‌ای که فوراً بسته می‌شود گم نشود.
if (-not (Test-Path $apiProject)) { throw "API project not found: $apiProject" }
if (-not (Test-Path $clientDir))  { throw "Client folder not found: $clientDir" }

# پیدا کردن ابزار: اول از PATH، و اگر نبود از محل‌های متداول نصب.
#
# چرا: ترمینال VS Code گاهی نسخه‌ی قدیمیِ PATH را نگه می‌دارد (پنجره‌ای که پیش از نصب یا
# به‌روزرسانی باز شده) و آن‌وقت «dotnet is not recognized» می‌دهد، در حالی که واقعاً نصب است.
# اسکریپت نباید به سالم بودنِ PATHِ نشست وابسته باشد.
function Resolve-Tool([string]$name, [string[]]$fallbacks) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    foreach ($p in $fallbacks) { if ($p -and (Test-Path $p)) { return $p } }
    return $null
}

$dotnetExe = Resolve-Tool 'dotnet' @(
    "$env:ProgramFiles\dotnet\dotnet.exe",
    "${env:ProgramFiles(x86)}\dotnet\dotnet.exe",
    "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
)

# ‎npm.cmd‎ نه ‎npm‎: وقتی با مسیر کامل صدا زده می‌شود، نسخه‌ی ‎.cmd‎ از هر پوسته‌ای اجرا می‌شود.
$npmExe = Resolve-Tool 'npm.cmd' @(
    "$env:ProgramFiles\nodejs\npm.cmd",
    "$env:LOCALAPPDATA\Programs\nodejs\npm.cmd"
)

if (-not $dotnetExe) { throw 'dotnet not found. Install the .NET SDK, or open a NEW terminal so PATH is refreshed.' }
if (-not $npmExe)    { throw 'npm not found. Install Node.js, or open a NEW terminal so PATH is refreshed.' }

# پوشه‌ی ابزارها به ابتدای PATHِ پنجره‌های فرزند اضافه می‌شود؛ وگرنه اگر PATHِ نشستِ فعلی
# ناقص باشد همان نقص به پنجره‌های تازه ارث می‌رسد و مثلاً vite نمی‌تواند node را پیدا کند.
$pathPrefix = (@((Split-Path -Parent $dotnetExe), (Split-Path -Parent $npmExe)) | Select-Object -Unique) -join ';'

# پورت‌ها طبق launchSettings.json و ‎hseq-client/.env.development‎ ثابت‌اند؛
# اگر اشغال باشند dotnet با خطای bind می‌ایستد و vite روی پورت دیگری بالا می‌آید
# که آدرسش با VITE_API_BASE_URL جور در نمی‌آید.
function Test-PortBusy([int]$port) {
    return [bool](Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)
}

# نصب وابستگی‌های کلاینت فقط در صورت نبودِ node_modules (اجرای دوباره‌ی npm install وقت تلف می‌کند).
if (-not (Test-Path (Join-Path $clientDir 'node_modules'))) {
    Write-Host 'installing client dependencies...' -ForegroundColor Yellow
    & $npmExe --prefix $clientDir install
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
}

# هر سرویس در پنجره‌ی خودش، تا لاگ‌ها قاطی نشوند و بستن یکی دیگری را نکُشد.
# ‎-NoExit‎ پنجره را پس از توقف سرویس باز نگه می‌دارد تا پیام خطا خوانده شود.
$psHost = (Get-Process -Id $PID).Path

# سرویسی که پورتش اشغال است دوباره اجرا نمی‌شود. اجرای دوباره‌ی اسکریپت بدون این بررسی،
# دو پنجره‌ی بی‌فایده می‌ساخت: نمونه‌ی دوم API اصلاً bind نمی‌شد، و vite بی‌سروصدا روی
# پورت بعدی (۵۱۷۴) بالا می‌آمد - جایی که کاربر به‌اشتباه سراغش می‌رفت.
function Start-DevService([string]$name, [int]$port, [string]$workDir, [string]$command) {
    if (Test-PortBusy $port) {
        Write-Host "$name is already running on port $port - skipped." -ForegroundColor DarkYellow
        return
    }

    Write-Host "starting $name..." -ForegroundColor Cyan
    Start-Process -FilePath $psHost -WorkingDirectory $workDir -ArgumentList @(
        # PATH پنجره‌ی فرزند پیش از هر چیز اصلاح می‌شود، بعد خودِ سرویس اجرا.
        '-NoExit', '-Command', "`$env:PATH = '$pathPrefix;' + `$env:PATH; $command"
    )
}

Start-DevService -name 'API'    -port 6270 -workDir $root      -command "& '$dotnetExe' run --project '$apiProject'"
Start-DevService -name 'client' -port 5173 -workDir $clientDir -command "& '$npmExe' run dev"

Write-Host ''
Write-Host 'API     -> http://localhost:6270  (swagger at /)' -ForegroundColor Green
Write-Host 'Client  -> http://localhost:5173' -ForegroundColor Green
Write-Host 'برای توقف، پنجره‌ی هر سرویس را ببندید یا در آن Ctrl+C بزنید.' -ForegroundColor DarkGray
