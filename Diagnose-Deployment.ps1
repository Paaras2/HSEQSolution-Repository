<#
.SYNOPSIS
    بررسی یک استقرار موجودِ HSEQ روی IIS و گزارش اینکه دقیقاً چه چیزی سر جایش نیست.

.DESCRIPTION
    این اسکریپت هیچ چیزی را تغییر نمی‌دهد - فقط می‌خواند و گزارش می‌دهد.

    فرقش با Deploy-Production.ps1 این است که آن اسکریپت پیش از استقرار بررسی می‌کند
    و مسیرهای خودش را فرض می‌گیرد؛ این یکی سراغ سایتی می‌رود که همین حالا روی IIS
    هست - چه با اسکریپت ساخته شده باشد چه دستی - و مسیرها را از خودِ IIS می‌خواند،
    نه از فرض.

    در پایان فهرستی از «کارهای بعدی» می‌دهد که به ترتیب اهمیت مرتب شده‌اند.

.PARAMETER SiteName
    نام سایت در IIS.

.PARAMETER SqlServer
    اگر داده شود، اتصال SQL با همین میزبان آزمایش می‌شود. در غیر این صورت میزبان از
    رشته‌ی اتصالِ داخل appsettings.Production.json برداشته می‌شود.

.PARAMETER DatabaseName
    نام دیتابیسی که ساختارش بررسی می‌شود.

.EXAMPLE
    .\Diagnose-Deployment.ps1 -SiteName 'HSEQTest'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SiteName,
    [string]$SqlServer = '',
    [string]$DatabaseName = 'HSEQDb'
)

Set-StrictMode -Version Latest
# عمداً Continue، نه Stop: هدفِ این اسکریپت گزارشِ کامل است. با Stop، اولین ایرادِ
# یک بررسی جلوی دیده شدن بقیه را می‌گرفت - یعنی دقیقاً همان رفت‌وبرگشتی که قرار
# است حذف شود.
$ErrorActionPreference = 'Continue'

$script:StepNumber = 0
$script:Todo = @()

function Write-Step { param([string]$m) $script:StepNumber++; Write-Host ''; Write-Host ("[{0,2}] {1}" -f $script:StepNumber, $m) -ForegroundColor Cyan }
function Write-Ok   { param([string]$m) Write-Host "     OK    $m" -ForegroundColor Green }
function Write-Warn { param([string]$m) Write-Host "     !     $m" -ForegroundColor Yellow }
function Write-Info { param([string]$m) Write-Host "           $m" -ForegroundColor DarkGray }
function Write-Fail {
    param([string]$m, [string]$Fix = '')
    Write-Host "     FAIL  $m" -ForegroundColor Red
    if ($Fix) { $script:Todo += $Fix }
}

Write-Host ''
Write-Host "تشخیص استقرار HSEQ - سایت '$SiteName'" -ForegroundColor White
Write-Host 'این اسکریپت هیچ تغییری اعمال نمی‌کند.' -ForegroundColor DarkGray

# ---------------------------------------------------------------------------
Write-Step 'دسترسی مدیر'
# ---------------------------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    Write-Ok 'با دسترسی مدیر اجرا شده'
} else {
    Write-Fail 'بدون دسترسی مدیر - خواندن پیکربندی IIS ممکن نیست.' `
               'PowerShell را با Run as Administrator باز کنید و دوباره اجرا کنید.'
    Write-Host ''
    Write-Host 'بدون دسترسی مدیر ادامه بی‌معناست.' -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
Write-Step 'IIS و ماژول‌ها'
# ---------------------------------------------------------------------------
$iisOk = $false
$globalModules = @()
try {
    Import-Module WebAdministration -ErrorAction Stop
    $globalModules = @(Get-WebGlobalModule -ErrorAction Stop)
    $iisOk = $true
    Write-Ok 'IIS در دسترس است'
} catch {
    Write-Fail "IIS در دسترس نیست: $($_.Exception.Message)" 'IIS را با نقش Web Server نصب کنید.'
}

if ($iisOk) {
    if ($globalModules | Where-Object { $_.Name -eq 'AspNetCoreModuleV2' }) {
        Write-Ok 'AspNetCoreModuleV2 نصب است'
    } else {
        Write-Fail 'AspNetCoreModuleV2 نیست - بک‌اند اصلاً بالا نمی‌آید.' `
                   'ASP.NET Core 10 Hosting Bundle را نصب کنید، سپس IIS را ری‌استارت کنید (iisreset).'
    }
    if ($globalModules | Where-Object { $_.Name -eq 'RewriteModule' }) {
        Write-Ok 'URL Rewrite نصب است'
    } else {
        Write-Fail 'URL Rewrite نیست - رفرش روی مسیرهای داخلی برنامه ۴۰۴ می‌دهد.' `
                   'URL Rewrite Module را نصب کنید.'
    }
}

# ---------------------------------------------------------------------------
Write-Step 'زمان اجرای .NET'
# ---------------------------------------------------------------------------
$runtimes = @(& dotnet --list-runtimes 2>$null)
if ($runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 10\.' }) {
    Write-Ok 'ASP.NET Core Runtime 10 موجود است'
} else {
    Write-Fail 'ASP.NET Core Runtime 10 پیدا نشد.' 'Hosting Bundle نسخه ۱۰ را نصب کنید.'
}

# ---------------------------------------------------------------------------
Write-Step 'سایت و بایندینگ‌ها'
# ---------------------------------------------------------------------------
$site = $null
$testUrls = @()
if ($iisOk) {
    $site = Get-Website | Where-Object { $_.Name -eq $SiteName }
    if (-not $site) {
        Write-Fail "سایتی به نام '$SiteName' در IIS نیست." `
                   "نام درست سایت را با Get-Website ببینید و همان را به -SiteName بدهید."
    } else {
        Write-Ok "سایت پیدا شد - وضعیت: $($site.State)"
        if ($site.State -ne 'Started') {
            Write-Fail 'سایت روشن نیست.' "Start-Website -Name '$SiteName'"
        }

        foreach ($b in $site.Bindings.Collection) {
            $parts = $b.bindingInformation -split ':', 3
            if ($parts.Count -ne 3) { continue }
            $bIp, $bPort, $bHost = $parts
            $proto = $b.protocol

            if ($bHost) {
                Write-Warn "بایندینگ $proto روی پورت $bPort با host header '$bHost'"
                Write-Info "فقط با همین نام جواب می‌دهد. صدا زدن با IP خطای ۴۰۰ می‌گیرد."
                $testUrls += "${proto}://${bHost}:${bPort}"
            } else {
                Write-Ok "بایندینگ $proto روی پورت $bPort بدون host header (با IP هم کار می‌کند)"
                $hostForUrl = if ($bIp -and $bIp -ne '*') { $bIp } else { 'localhost' }
                $testUrls += "${proto}://${hostForUrl}:${bPort}"
            }
        }
        if (-not $testUrls) { Write-Fail 'سایت هیچ بایندینگی ندارد.' 'در IIS Manager یک بایندینگ http اضافه کنید.' }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'فایل‌های کلاینت'
# ---------------------------------------------------------------------------
$sitePath = ''
if ($site) {
    $sitePath = $site.physicalPath -replace '%SystemDrive%', $env:SystemDrive
    Write-Info "مسیر فیزیکی سایت: $sitePath"

    if (-not (Test-Path $sitePath)) {
        Write-Fail "مسیر فیزیکی سایت وجود ندارد: $sitePath" `
                   "پوشه را بسازید و محتویات Frontend\ بسته را داخلش بریزید."
    } else {
        $indexPath = Join-Path $sitePath 'index.html'
        if (Test-Path $indexPath) {
            Write-Ok 'index.html موجود است'
        } else {
            Write-Fail 'index.html در مسیر سایت نیست - همین باعث خطای 403.14 می‌شود.' `
                       "محتویات (نه خودِ پوشه) Frontend\ بسته را در $sitePath کپی کنید."
        }

        $webConfigPath = Join-Path $sitePath 'web.config'
        if (Test-Path $webConfigPath) {
            $wc = Get-Content $webConfigPath -Raw
            if ($wc -match 'rewrite') {
                Write-Ok 'web.config با قوانین rewrite موجود است'
            } else {
                Write-Warn 'web.config هست ولی قانون rewrite ندارد - رفرش روی مسیرهای داخلی ۴۰۴ می‌دهد.'
            }
        } else {
            Write-Fail 'web.config در مسیر سایت نیست.' `
                       "web.config را هم از Frontend\ بسته کپی کنید (مسیریابی SPA به آن وابسته است)."
        }

        if (Test-Path (Join-Path $sitePath 'assets')) { Write-Ok 'پوشه‌ی assets موجود است' }
        else { Write-Warn 'پوشه‌ی assets نیست - صفحه بدون CSS و JS بالا می‌آید.' }

        # فایل‌های بک‌اند نباید داخل پوشه‌ی کلاینت باشند: از راه وب قابل دانلودند.
        if (Test-Path (Join-Path $sitePath 'HSEQ.API.dll')) {
            Write-Fail 'فایل‌های بک‌اند داخل پوشه‌ی کلاینت‌اند - appsettings.Production.json از راه وب قابل دانلود است.' `
                       'بک‌اند را به یک پوشه‌ی جدا بیرون از مسیر سایت منتقل کنید.'
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'Application با مسیر /api'
# ---------------------------------------------------------------------------
$apiPath = ''
$apiApp = $null
if ($site) {
    $apiApp = Get-WebApplication -Site $SiteName | Where-Object { $_.path -eq '/api' }
    if (-not $apiApp) {
        Write-Fail 'Application با مسیر /api ثبت نشده - همه‌ی فراخوان‌های کلاینت ۴۰۴ می‌گیرند.' `
                   "New-WebApplication -Site '$SiteName' -Name 'api' -PhysicalPath '<مسیر بک‌اند>' -ApplicationPool '<app pool>'"
    } else {
        $apiPath = $apiApp.PhysicalPath -replace '%SystemDrive%', $env:SystemDrive
        Write-Ok "ثبت شده - مسیر: $apiPath"

        if (-not (Test-Path $apiPath)) {
            Write-Fail "مسیر بک‌اند وجود ندارد: $apiPath" "محتویات Backend\ بسته را در این مسیر کپی کنید."
        } else {
            if (Test-Path (Join-Path $apiPath 'HSEQ.API.dll')) { Write-Ok 'HSEQ.API.dll موجود است' }
            else { Write-Fail 'HSEQ.API.dll در مسیر بک‌اند نیست.' "محتویات Backend\ بسته را در $apiPath کپی کنید." }

            if (Test-Path (Join-Path $apiPath 'web.config')) { Write-Ok 'web.config بک‌اند موجود است' }
            else { Write-Fail 'web.config بک‌اند نیست - بدون آن AspNetCoreModule برنامه را اجرا نمی‌کند.' "web.config را از Backend\ بسته کپی کنید." }
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'Application Pool'
# ---------------------------------------------------------------------------
$poolIdentity = ''
if ($site) {
    $poolName = $site.applicationPool
    Write-Info "نام: $poolName"
    $pool = Get-Item "IIS:\AppPools\$poolName" -ErrorAction SilentlyContinue
    if (-not $pool) {
        Write-Fail "Application Pool '$poolName' پیدا نشد."
    } else {
        if ($pool.state -eq 'Started') { Write-Ok 'روشن است' }
        else { Write-Fail "خاموش است (وضعیت: $($pool.state))." "Start-WebAppPool -Name '$poolName'" }

        # ASP.NET Core خودش میزبان است؛ بارگذاری CLR باعث خطای 500.19/500.30 می‌شود.
        if ([string]::IsNullOrEmpty($pool.managedRuntimeVersion)) {
            Write-Ok '.NET CLR Version روی No Managed Code است'
        } else {
            Write-Fail ".NET CLR Version روی '$($pool.managedRuntimeVersion)' است، باید No Managed Code باشد." `
                       "Set-ItemProperty 'IIS:\AppPools\$poolName' -Name managedRuntimeVersion -Value ''"
        }

        $poolIdentity = "IIS AppPool\$poolName"
        Write-Info "هویت برای دسترسی فایل: $poolIdentity"

        # اگر /api زیر app pool دیگری باشد، تنظیم CLR بالا اصلاً روی آن اثر ندارد.
        if ($apiApp -and $apiApp.applicationPool -ne $poolName) {
            Write-Warn "Application با مسیر /api زیر app pool دیگری است: $($apiApp.applicationPool)"
            Write-Info 'بررسی‌های این بخش مربوط به app pool سایت است، نه آن یکی.'
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'فایل تنظیمات عملیاتی'
# ---------------------------------------------------------------------------
$config = $null
$connectionString = ''
if ($apiPath -and (Test-Path $apiPath)) {
    $cfgPath = Join-Path $apiPath 'appsettings.Production.json'
    if (-not (Test-Path $cfgPath)) {
        Write-Fail "appsettings.Production.json کنار HSEQ.API.dll نیست - برنامه بالا نمی‌آید." `
                   "فایل تنظیمات را در $apiPath بگذارید."
    } else {
        Write-Ok 'فایل موجود است'
        try {
            $config = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {
            Write-Fail "فایل JSON معتبر نیست: $($_.Exception.Message)" 'ساختار JSON فایل تنظیمات را اصلاح کنید.'
        }

        if ($config) {
            # مقادیر عمداً چاپ نمی‌شوند - فقط نام کلید - تا کلید امضا و رشته‌ی اتصال
            # داخل خروجی و اسکرین‌شات ننشیند.
            $required = @(
                @{ Path = 'ConnectionStrings.DefaultConnection'; Label = 'رشته‌ی اتصال' }
                @{ Path = 'Jwt.Key';                             Label = 'کلید JWT' }
                @{ Path = 'Jwt.Issuer';                          Label = 'Jwt:Issuer' }
                @{ Path = 'Jwt.Audience';                        Label = 'Jwt:Audience' }
                @{ Path = 'Jwt.ExpiryInMinutes';                 Label = 'Jwt:ExpiryInMinutes' }
                @{ Path = 'UploadPath';                          Label = 'UploadPath' }
                @{ Path = 'UserManagementAPI.Url';               Label = 'نشانی سرویس UM' }
            )
            foreach ($r in $required) {
                $val = $config
                foreach ($seg in $r.Path -split '\.') {
                    if ($null -ne $val -and $val.PSObject.Properties.Name -contains $seg) { $val = $val.$seg }
                    else { $val = $null; break }
                }
                if ($null -eq $val -or [string]::IsNullOrWhiteSpace([string]$val)) {
                    Write-Fail "$($r.Label) مقدار ندارد." "کلید $($r.Path) را در appsettings.Production.json پر کنید."
                } elseif ([string]$val -match '<[^>]+>') {
                    Write-Fail "$($r.Label) هنوز جای‌نگهدار دارد." "مقدار واقعی $($r.Path) را جایگزین کنید."
                } else {
                    Write-Ok "$($r.Label) پر شده"
                }
            }

            if ($config.PSObject.Properties.Name -contains 'ConnectionStrings') {
                $connectionString = [string]$config.ConnectionStrings.DefaultConnection
                if ($connectionString -match '\(localdb\)') {
                    Write-Fail 'رشته‌ی اتصال به LocalDB اشاره می‌کند - موتوری فقط برای توسعه.' `
                               'میزبان SQL Server واقعی را در رشته‌ی اتصال بگذارید.'
                }
            }

            # مبدأ CORS باید دقیقاً با همان چیزی که مرورگر می‌بیند یکی باشد.
            $origins = @()
            if ($config.PSObject.Properties.Name -contains 'Cors' -and
                $config.Cors.PSObject.Properties.Name -contains 'AllowedOrigins') {
                $origins = @($config.Cors.AllowedOrigins)
            }
            if ($origins.Count -eq 0) {
                Write-Fail 'Cors:AllowedOrigins خالی است - برنامه در محیط عملیاتی بالا نمی‌آید.' `
                           'مبدأ سایت را در Cors:AllowedOrigins بگذارید، مثلاً http://<IP>:<PORT>'
            } else {
                Write-Ok "Cors:AllowedOrigins = $($origins -join ', ')"
                foreach ($u in $testUrls) {
                    if ($origins -notcontains $u) {
                        Write-Warn "مبدأ '$u' (از بایندینگ سایت) در فهرست CORS نیست."
                    }
                }
            }
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'مسیرهای داده و دسترسی نوشتن'
# ---------------------------------------------------------------------------
if ($config -and $config.PSObject.Properties.Name -contains 'UploadPath') {
    $uploadPath = [string]$config.UploadPath
    if ($uploadPath -match '<[^>]+>') {
        Write-Warn 'UploadPath هنوز جای‌نگهدار دارد؛ بررسی دسترسی رد شد.'
    } elseif (-not (Test-Path $uploadPath)) {
        Write-Fail "مسیر ذخیره‌ی مدارک وجود ندارد: $uploadPath" `
                   "New-Item -ItemType Directory -Force '$uploadPath'"
    } else {
        Write-Ok "مسیر مدارک موجود است: $uploadPath"

        # آزمایش واقعی نوشتن. مجوزها آن‌قدر لایه دارند که خواندن ACL به‌تنهایی
        # جواب قطعی نمی‌دهد؛ ولی این با هویت اجراکننده تست می‌شود نه app pool.
        try {
            $probe = Join-Path $uploadPath ".hseq_write_probe_$([guid]::NewGuid().ToString('N'))"
            [void](New-Item -ItemType File -Path $probe -ErrorAction Stop)
            Remove-Item $probe -Force -ErrorAction SilentlyContinue
            Write-Ok 'نوشتن در این مسیر ممکن است (با هویت شما، نه app pool)'
        } catch {
            Write-Warn "نوشتن با هویت شما ممکن نشد: $($_.Exception.Message)"
        }

        if ($poolIdentity) {
            $acl = Get-Acl $uploadPath
            $rule = $acl.Access | Where-Object {
                $_.IdentityReference.Value -eq $poolIdentity -and
                $_.FileSystemRights -match 'Modify|FullControl|Write'
            }
            if ($rule) {
                Write-Ok "هویت app pool دسترسی نوشتن دارد"
            } else {
                Write-Fail "هویت '$poolIdentity' روی مسیر مدارک دسترسی نوشتن ندارد - آپلود سند شکست می‌خورد." `
                           "icacls `"$uploadPath`" /grant `"${poolIdentity}:(OI)(CI)M`""
            }
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'اتصال SQL Server'
# ---------------------------------------------------------------------------
$sqlTarget = $SqlServer
if (-not $sqlTarget -and $connectionString -match 'Server\s*=\s*([^;]+)') { $sqlTarget = $Matches[1].Trim() }

if (-not $sqlTarget -or $sqlTarget -match '<[^>]+>') {
    Write-Warn 'میزبان SQL مشخص نیست؛ این بررسی رد شد.'
} else {
    Write-Info "میزبان: $sqlTarget"
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection
        $conn.ConnectionString = "Server=$sqlTarget;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
        $conn.Open()

        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = 'HSEQDb'"
        $dbExists = [int]$cmd.ExecuteScalar()
        $conn.Close()

        Write-Ok 'اتصال به SQL Server برقرار شد (با هویت شما)'
        if ($dbExists -eq 1) {
            Write-Ok "دیتابیس HSEQDb موجود است"

            # وجود دیتابیس یعنی اتصال برقرار است، نه اینکه ساختارش ساخته شده.
            # /api/Health هم فقط CanConnectAsync را می‌سنجد، پس روی دیتابیسِ خالی
            # پاسخ Healthy می‌دهد در حالی که اولین ورود با ۵۰۰ شکست می‌خورد: مسیر
            # ورود نقش کاربر را از جدول Admins می‌خواند و آن جدول وجود ندارد.
            # این دقیقاً همان حالتی است که از بیرون قابل تشخیص نبود.
            $conn2 = New-Object System.Data.SqlClient.SqlConnection
            $conn2.ConnectionString = "Server=$sqlTarget;Database=$DatabaseName;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
            try {
                $conn2.Open()
                $c2 = $conn2.CreateCommand()
                $c2.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE name = 'Admins'),
    (SELECT COUNT(*) FROM sys.tables),
    (SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory')
"@
                $r = $c2.ExecuteReader()
                $null = $r.Read()
                $adminsTable = [int]$r.GetValue(0)
                $tableCount  = [int]$r.GetValue(1)
                $historyTable = [int]$r.GetValue(2)
                $r.Close()

                if ($tableCount -eq 0) {
                    Write-Fail 'دیتابیس خالی است - هیچ جدولی ندارد.' `
                               "Database\migrations.sql بسته را روی HSEQDb اجرا کنید (اسکریپت idempotent است)."
                } elseif ($adminsTable -eq 0) {
                    Write-Fail "جدول Admins وجود ندارد (فقط $tableCount جدول هست) - ورود کاربر با ۵۰۰ شکست می‌خورد." `
                               "Database\migrations.sql را روی HSEQDb اجرا کنید."
                } else {
                    Write-Ok "ساختار دیتابیس ساخته شده ($tableCount جدول، از جمله Admins)"

                    # تعداد مهاجرت‌های اعمال‌شده: اگر تاریخچه خالی باشد، جدول‌ها به
                    # روشی خارج از EF ساخته شده‌اند و استقرار بعدی دوباره تلاش می‌کند.
                    if ($historyTable -eq 1) {
                        $c3 = $conn2.CreateCommand()
                        $c3.CommandText = 'SELECT COUNT(*) FROM [__EFMigrationsHistory]'
                        $applied = [int]$c3.ExecuteScalar()
                        Write-Info "مهاجرت‌های اعمال‌شده: $applied"
                    }

                    $c4 = $conn2.CreateCommand()
                    $c4.CommandText = 'SELECT COUNT(*) FROM [Admins]'
                    $adminRows = [int]$c4.ExecuteScalar()
                    if ($adminRows -eq 0) {
                        Write-Warn 'جدول Admins خالی است - هیچ‌کس نقش «مدیر سیستم» ندارد.'
                        Write-Info 'ورود کار می‌کند ولی همه فقط «مشاهده» خواهند بود.'
                    } else {
                        Write-Ok "جدول Admins $adminRows ردیف دارد"
                    }
                }
            } catch {
                Write-Fail "خواندن ساختار HSEQDb ناموفق: $($_.Exception.Message)"
            } finally {
                if ($conn2.State -eq 'Open') { $conn2.Close() }
            }
        } else {
            Write-Fail 'دیتابیس HSEQDb روی این سرور نیست.' `
                       "یک دیتابیس خالی HSEQDb بسازید، سپس Database\migrations.sql را روی آن اجرا کنید."
        }
    } catch {
        Write-Fail "اتصال به SQL ناموفق: $($_.Exception.Message)" `
                   'میزبان، فایروال پورت ۱۴۳۳ و دسترسی هویت Application Pool به SQL را بررسی کنید.'
    }
    Write-Info 'توجه: این اتصال با هویت شما بود. app pool با هویت دیگری وصل می‌شود.'
}

# ---------------------------------------------------------------------------
Write-Step 'دسترسی به سرویس User Management'
# ---------------------------------------------------------------------------
if ($config -and $config.PSObject.Properties.Name -contains 'UserManagementAPI') {
    $umUrl = [string]$config.UserManagementAPI.Url
    if ($umUrl -match '<[^>]+>') {
        Write-Warn 'نشانی UM هنوز جای‌نگهدار دارد.'
    } else {
        try {
            $uri = [Uri]$umUrl
            $umPort = if ($uri.Port -gt 0) { $uri.Port } else { 80 }
            $tcp = Test-NetConnection -ComputerName $uri.Host -Port $umPort -WarningAction SilentlyContinue
            if ($tcp.TcpTestSucceeded) {
                Write-Ok "سرویس UM در دسترس است ($($uri.Host):$umPort)"
            } else {
                Write-Fail "به سرویس UM نمی‌رسیم ($($uri.Host):$umPort) - هیچ کاربری نمی‌تواند وارد شود." `
                           'قوانین فایروال خروجی و در دسترس بودن سرویس UM را بررسی کنید.'
            }
        } catch {
            Write-Warn "بررسی UM ممکن نشد: $($_.Exception.Message)"
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'پاسخ واقعی HTTP'
# ---------------------------------------------------------------------------
function Get-Status {
    param(
        [string]$Url,
        [string]$Method = 'GET'
    )
    try {
        $r = Invoke-WebRequest -Uri $Url -Method $Method -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
        return [int]$r.StatusCode
    } catch {
        if ($_.Exception.PSObject.Properties.Name -contains 'Response' -and $_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        return 0
    }
}

if ($testUrls.Count -eq 0) {
    Write-Warn 'بایندینگی برای آزمایش نبود.'
} else {
    foreach ($base in ($testUrls | Select-Object -Unique)) {
        Write-Info "--- $base"

        $rootStatus = Get-Status "$base/"
        switch ($rootStatus) {
            200     { Write-Ok   "  /  →  ۲۰۰ - کلاینت سرو می‌شود" }
            403     { Write-Fail "  /  →  ۴۰۳ - index.html در مسیر سایت نیست." "محتویات Frontend\ بسته را در مسیر سایت کپی کنید." }
            400     { Write-Fail "  /  →  ۴۰۰ - عدم تطابق host header." 'فیلد Host name بایندینگ را خالی کنید یا با همان نام صدا بزنید.' }
            0       { Write-Fail "  /  →  اتصال برقرار نشد." 'روشن بودن سایت و باز بودن پورت در فایروال را بررسی کنید.' }
            default { Write-Warn "  /  →  $rootStatus" }
        }

        $healthStatus = Get-Status "$base/api/Health"
        switch ($healthStatus) {
            200     { Write-Ok   "  /api/Health  →  ۲۰۰ - برنامه و دیتابیس هر دو سالم" }
            503     { Write-Fail "  /api/Health  →  ۵۰۳ - برنامه بالاست ولی به دیتابیس نمی‌رسد." 'رشته‌ی اتصال و دسترسی هویت Application Pool به SQL را بررسی کنید.' }
            404     { Write-Fail "  /api/Health  →  ۴۰۴ - Application با مسیر /api ثبت نشده یا بک‌اند بالا نیامده." "Application با مسیر /api را بسازید و فایل‌های Backend\ را در مسیرش بگذارید." }
            500     { Write-Fail "  /api/Health  →  ۵۰۰ - برنامه بالا نیامد (به احتمال زیاد تنظیمات)." 'لاگ stdout را روشن کنید - پایین توضیح داده شده.' }
            0       { Write-Fail "  /api/Health  →  اتصال برقرار نشد." }
            default { Write-Warn "  /api/Health  →  $healthStatus" }
        }

        # مسیر دوتایی باید ۴۰۴ بدهد. پیشوند /api یک مالک دارد - خودِ IIS، از راه
        # Application زیر همان مسیر - پس هیچ کنترلری دوباره «api» را اعلام نمی‌کند.
        # پاسخ دادنِ این نشانی یعنی بسته‌ای قدیمی‌تر از آن اصلاح مستقر شده، و در آن
        # حالت مسیری که کلاینت صدا می‌زند (/api/Auth/login) ۴۰۴ می‌گیرد.
        $doubledStatus = Get-Status "$base/api/api/Health"
        if ($doubledStatus -in 200, 503) {
            Write-Fail "  /api/api/Health  →  $doubledStatus - باید ۴۰۴ باشد؛ پیشوند /api دو مالک دارد." `
                       'بسته‌ی به‌روز را با Publish-Production.ps1 بسازید و دوباره استقرار دهید. با این چیدمان ورود کاربران کار نمی‌کند.'
        } else {
            Write-Ok "  /api/api/Health  →  $doubledStatus - درست است، مسیر دوتایی وجود ندارد"
        }

        # خودِ مسیر ورود. بررسی سلامت می‌تواند سالم باشد در حالی که مسیر ورود نباشد -
        # خرابی‌ای که روی سرور دیده شد دقیقاً همین شکل بود.
        # بدنه‌ی خالی: هیچ اعتبارنامه‌ای لازم نیست و هیچ تلاش ورودی هم ثبت نمی‌شود.
        $loginStatus = Get-Status "$base/api/Auth/login" -Method 'POST'
        switch ($loginStatus) {
            404     { Write-Fail "  POST /api/Auth/login  →  ۴۰۴ - هیچ کاربری نمی‌تواند وارد شود." 'مسیر Application در IIS و تازه بودن بسته را بررسی کنید.' }
            500     { Write-Fail "  POST /api/Auth/login  →  ۵۰۰ - خطای مدیریت‌نشده." 'لاگ stdout بک‌اند را ببینید.' }
            0       { Write-Fail "  POST /api/Auth/login  →  اتصال برقرار نشد." }
            default { Write-Ok   "  POST /api/Auth/login  →  $loginStatus - به اکشن می‌رسد" }
        }
    }
}

# ---------------------------------------------------------------------------
Write-Step 'لاگ عیب‌یابی'
# ---------------------------------------------------------------------------
if ($apiPath -and (Test-Path (Join-Path $apiPath 'web.config'))) {
    $wcRaw = Get-Content (Join-Path $apiPath 'web.config') -Raw
    if ($wcRaw -match 'stdoutLogFile="([^"]+)"') {
        $logTarget = $Matches[1]
        Write-Info "مسیر لاگ stdout: $logTarget"
        $logDir = Split-Path ($logTarget -replace '^\.\\', "$apiPath\") -Parent
        if (Test-Path $logDir) {
            $recent = Get-ChildItem $logDir -Filter 'stdout*' -ErrorAction SilentlyContinue |
                      Sort-Object LastWriteTime | Select-Object -Last 1
            if ($recent) {
                Write-Ok "آخرین لاگ: $($recent.Name)"
                Write-Info 'برای دیدن دلیل بالا نیامدن برنامه:'
                Write-Info "  Get-Content '$($recent.FullName)' -Tail 40"
            } else {
                Write-Info 'لاگی نوشته نشده (احتمالاً stdoutLogEnabled=false است).'
            }
        } else {
            Write-Warn "پوشه‌ی لاگ وجود ندارد: $logDir"
        }
    }
    if ($wcRaw -match 'stdoutLogEnabled="false"') {
        Write-Info 'لاگ stdout خاموش است. اگر برنامه بالا نمی‌آید، موقتاً true کنید،'
        Write-Info 'app pool را ری‌سایکل کنید، خطا را بخوانید و دوباره false کنید.'
    }
}

# ---------------------------------------------------------------------------
Write-Host ''
Write-Host '────────────────────────────────────────────────────────' -ForegroundColor DarkGray
if ($script:Todo.Count -eq 0) {
    Write-Host 'هیچ ایرادی پیدا نشد.' -ForegroundColor Green
    Write-Host 'اگر هنوز مشکلی هست، خروجی کامل بالا را بفرستید.'
    exit 0
} else {
    Write-Host "کارهای بعدی ($($script:Todo.Count) مورد) - به ترتیب:" -ForegroundColor Yellow
    Write-Host ''
    $i = 1
    foreach ($t in $script:Todo) { Write-Host "  $i. $t" -ForegroundColor White; $i++ }
    Write-Host ''
    exit 1
}
