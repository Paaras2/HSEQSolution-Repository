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

# پورت‌ها طبق launchSettings.json و ‎hseq-client/.env.development‎ ثابت‌اند؛
# اگر اشغال باشند dotnet با خطای bind می‌ایستد و vite روی پورت دیگری بالا می‌آید
# که آدرسش با VITE_API_BASE_URL جور در نمی‌آید.
function Test-PortBusy([int]$port) {
    return [bool](Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)
}

foreach ($p in @(6270, 5173)) {
    if (Test-PortBusy $p) {
        Write-Host "warning: port $p is already in use - a previous dev server may still be running." -ForegroundColor Yellow
    }
}

# نصب وابستگی‌های کلاینت فقط در صورت نبودِ node_modules (اجرای دوباره‌ی npm install وقت تلف می‌کند).
if (-not (Test-Path (Join-Path $clientDir 'node_modules'))) {
    Write-Host 'installing client dependencies...' -ForegroundColor Yellow
    npm --prefix $clientDir install
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
}

# هر سرویس در پنجره‌ی خودش، تا لاگ‌ها قاطی نشوند و بستن یکی دیگری را نکُشد.
# ‎-NoExit‎ پنجره را پس از توقف سرویس باز نگه می‌دارد تا پیام خطا خوانده شود.
$psHost = (Get-Process -Id $PID).Path

Write-Host 'starting API...' -ForegroundColor Cyan
Start-Process -FilePath $psHost -WorkingDirectory $root -ArgumentList @(
    '-NoExit', '-Command', "dotnet run --project `"$apiProject`""
)

Write-Host 'starting client...' -ForegroundColor Cyan
Start-Process -FilePath $psHost -WorkingDirectory $clientDir -ArgumentList @(
    '-NoExit', '-Command', 'npm run dev'
)

Write-Host ''
Write-Host 'API     -> http://localhost:6270  (swagger at /)' -ForegroundColor Green
Write-Host 'Client  -> http://localhost:5173' -ForegroundColor Green
Write-Host 'برای توقف، پنجره‌ی هر سرویس را ببندید یا در آن Ctrl+C بزنید.' -ForegroundColor DarkGray
