#Requires -Version 7.0
# PowerShell 7 لازم است (اپراتور ?? در همین فایل استفاده شده). با
# powershell.exe نسخه‌ی ۵.۱ فقط یک خطای پارس مبهم می‌دهد؛ با این خط،
# پیام صریح می‌گیرید. اجرا با: pwsh -File .\Start-UmSimulator.ps1
<#
.SYNOPSIS
    شبیه‌ساز محلی سرویس User Management - فقط برای آزمایش، هرگز روی سرور عملیاتی.

.DESCRIPTION
    وقتی ماشین به شبکه‌ی سازمان وصل نیست، سرویس واقعی UM
    (https://usermanagement.odcc.ir/api/) در دسترس نیست و هیچ‌کس نمی‌تواند وارد شود -
    یعنی صفحه‌ی ورود و کل جریان احراز هویت قابل آزمایش نیست.

    این اسکریپت همان یک اندپوینتی را که HSEQ.API واقعاً صدا می‌زند شبیه‌سازی می‌کند،
    با همان شکلِ پاسخی که سرویس واقعی می‌دهد:

        POST {UmUrl}Auth/checkCredential   { "username", "password" }
          200  CheckCredentialDto
          400  { "message": "اطلاعات وارد شده صحیح نمی باشد", "code": 410 }

    بقیه‌ی زنجیره دست‌نخورده می‌ماند: توکن JWT را خودِ HSEQ.API می‌سازد و نقش کاربر
    را از جدول Admins همان دیتابیس می‌خواند. یعنی چیزی که آزمایش می‌شود، جریان
    واقعیِ ورود است، نه یک میان‌بر.

.PARAMETER Port
    پورت محلی. پیش‌فرض 8899.

.PARAMETER Hostname
    نامی که شبیه‌ساز روی آن گوش می‌دهد. پیش‌فرض 'localhost' که بدون دسترسی مدیر
    کار می‌کند.

    اگر برنامه را در محیط Production آزمایش می‌کنید، این را عوض کنید:
    ProductionConfigurationValidator عمداً نشانی 'localhost' و '127.0.0.1' را برای
    سرویس UM رد می‌کند (چون در عمل همیشه روی میزبان دیگری است و دیدنِ نشانی محلی
    یعنی تنظیمات جابه‌جا شده). این محافظ درست است و نباید ضعیفش کرد.

    راه درست: یک نام مستعار در فایل hosts بسازید که به لوپ‌بک اشاره کند -
        127.0.0.1   um-sim.local
    و شبیه‌ساز را با همان نام اجرا کنید:
        .\Start-UmSimulator.ps1 -Hostname um-sim.local
    این‌طور اعتبارسنجی پاس می‌شود ولی ترافیک همچنان از ماشین بیرون نمی‌رود.

    نکته: هر نامی غیر از 'localhost' نیاز به PowerShell با دسترسی Administrator
    دارد (محدودیت خودِ HttpListener در ویندوز).

.PARAMETER AllowedPcodes
    کدهای پرسنلی‌ای که «معتبر» شناخته می‌شوند. هر کد دیگری رد می‌شود.
    پیش‌فرض دو حساب مدیرِ موجود در دیتابیس است.

.PARAMETER Password
    اگر داده شود، فقط همین رمز پذیرفته می‌شود و هر رمز دیگری پاسخِ «اطلاعات وارد شده
    صحیح نمی باشد» می‌گیرد - برای آزمودن پیامِ رمز غلط. بدون آن هر رمز غیرخالی پذیرفته
    می‌شود.

.PARAMETER FirstLoginPcodes
    کدهایی که با isFirstLogin=true برمی‌گردند (کاربری که هنوز رمز پیش‌فرض دارد).

.PARAMETER InactivePcodes
    کدهایی که رمزشان پذیرفته می‌شود ولی با isActive=false برمی‌گردند.

.PARAMETER OutagePcodes
    کدهایی که به‌جای پاسخ، یک صفحه‌ی خطای HTML با کد ۵۰۳ می‌گیرند - مثل سامانه‌ای که
    پشت پروکسی خاموش است.

.EXAMPLE
    .\Start-UmSimulator.ps1
    سپس در appsettings.Production.json روی همین ماشین:
        "UserManagementAPI": { "Url": "http://localhost:8899/api/" }

.EXAMPLE
    .\Start-UmSimulator.ps1 -AllowedPcodes 3256,3548,7777,9999 -Password 'Test@1234' -FirstLoginPcodes 3548 -InactivePcodes 7777 -OutagePcodes 9999
    همه‌ی حالت‌های صفحه‌ی ورود با یک شبیه‌ساز.

.NOTES
    ⚠ بدون ‎-Password‎ این سرویس هر رمزی را می‌پذیرد. تنها محافظش این است که فقط روی
    loopback گوش می‌دهد و از بیرونِ ماشین اصلاً قابل دسترسی نیست.

    هرگز نباید در بسته‌ی انتشار برود یا روی سرور عملیاتی اجرا شود. اگر تنظیمات
    عملیاتی به این شبیه‌ساز اشاره کند، عملاً احراز هویت سامانه دور زده شده است.
#>
[CmdletBinding()]
param(
    [int]$Port = 8899,
    [string]$Hostname = 'localhost',
    [int[]]$AllowedPcodes = @(3256, 3548),
    [string]$Password,
    [int[]]$FirstLoginPcodes = @(),
    [int[]]$InactivePcodes = @(),
    [int[]]$OutagePcodes = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# عمداً نه '+' و نه '*': شبیه‌ساز فقط به نامی که صریحاً داده شده پاسخ می‌دهد.
# با نام پیش‌فرض (localhost) اصلاً از بیرونِ ماشین قابل دسترسی نیست؛ با نام مستعارِ
# hosts که به ۱۲۷.۰.۰.۱ اشاره کند هم همین‌طور.
$prefix = "http://${Hostname}:$Port/"

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($prefix)

try {
    $listener.Start()
} catch {
    $hint = ''
    if ($Hostname -ne 'localhost') {
        $hint = [Environment]::NewLine +
                'برای نامی غیر از localhost باید PowerShell را با دسترسی Administrator اجرا کنید.'
    }
    throw "شبیه‌ساز روی $prefix بالا نیامد: $($_.Exception.Message)$hint"
}

# همان متن و کدی که سرویس واقعی برای اعتبارنامه‌ی غلط برمی‌گرداند.
$wrongCredentials = '{"message":"اطلاعات وارد شده صحیح نمی باشد","code":410}'

Write-Host ''
Write-Host '  شبیه‌ساز سرویس UM - فقط آزمایشی' -ForegroundColor Yellow
Write-Host '  ────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host "  نشانی      : $prefix" -ForegroundColor White
Write-Host "  اندپوینت   : POST ${prefix}api/Auth/checkCredential"
Write-Host "  کدهای مجاز : $($AllowedPcodes -join ', ')"
if ($Password) {
    Write-Host '  رمز عبور   : فقط رمزِ داده‌شده با ‎-Password' -ForegroundColor White
} else {
    Write-Host '  رمز عبور   : هر مقدار غیرخالی پذیرفته می‌شود' -ForegroundColor Yellow
}
if ($FirstLoginPcodes) { Write-Host "  ورود اول   : $($FirstLoginPcodes -join ', ')" }
if ($InactivePcodes)   { Write-Host "  غیرفعال    : $($InactivePcodes -join ', ')" }
if ($OutagePcodes)     { Write-Host "  خاموش (۵۰۳): $($OutagePcodes -join ', ')" }
Write-Host ''
Write-Host '  در appsettings.Production.json این ماشین بگذارید:' -ForegroundColor Cyan
Write-Host "    `"UserManagementAPI`": { `"Url`": `"$prefix" + "api/`" }"
Write-Host ''
Write-Host '  ⚠ روی سرور عملیاتی اجرا نکنید و تنظیمات عملیاتی را به آن اشاره ندهید.' -ForegroundColor Red
Write-Host '  توقف: Ctrl+C' -ForegroundColor DarkGray
Write-Host ''

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response
        $response.ContentType = 'application/json; charset=utf-8'

        $path = $request.Url.AbsolutePath.TrimEnd('/')
        $stamp = (Get-Date -Format 'HH:mm:ss')

        if ($request.HttpMethod -ne 'POST' -or $path -notlike '*/Auth/checkCredential') {
            $response.StatusCode = 404
            $payload = '{"message":"not found","code":404}'
            Write-Host "  [$stamp] $($request.HttpMethod) $path -> 404" -ForegroundColor DarkGray
        }
        else {
            $body = ''
            if ($request.HasEntityBody) {
                $reader = [System.IO.StreamReader]::new($request.InputStream, $request.ContentEncoding)
                $body = $reader.ReadToEnd()
                $reader.Dispose()
            }

            $username = $null
            $providedPassword = $null
            try {
                $parsed = $body | ConvertFrom-Json
                # نام فیلدها همان LoginRequestModel است؛ تطبیق بدون حساسیت به بزرگی حروف.
                foreach ($p in $parsed.PSObject.Properties) {
                    if ($p.Name -ieq 'Username') { $username = [string]$p.Value }
                    if ($p.Name -ieq 'Password') { $providedPassword = [string]$p.Value }
                }
            } catch {
                $username = $null
            }

            $pcode = 0
            $isNumeric = [int]::TryParse(($username ?? ''), [ref]$pcode)
            $passwordMatches = -not [string]::IsNullOrEmpty($providedPassword) -and
                               (-not $Password -or $providedPassword -ceq $Password)

            if ($isNumeric -and $OutagePcodes -contains $pcode) {
                # شکلِ پروکسی‌ای که سامانه‌ی پشتش خاموش است: HTML، نه JSON.
                $response.StatusCode = 503
                $response.ContentType = 'text/html; charset=utf-8'
                $payload = '<!DOCTYPE html><html><body><h1>503 Service Unavailable</h1></body></html>'
                Write-Host "  [$stamp] خاموشیِ شبیه‌سازی‌شده برای $pcode -> 503" -ForegroundColor Magenta
            }
            elseif (-not $isNumeric -or $AllowedPcodes -notcontains $pcode -or -not $passwordMatches) {
                $response.StatusCode = 400
                $payload = $wrongCredentials
                Write-Host "  [$stamp] ورود ناموفق برای '$username' -> 400 (410)" -ForegroundColor Yellow
            }
            else {
                $response.StatusCode = 200
                $isActive = -not ($InactivePcodes -contains $pcode)
                $isFirstLogin = $FirstLoginPcodes -contains $pcode
                $dto = [ordered]@{
                    pCode                = $pcode
                    firstName            = 'کاربر'
                    lastName             = 'آزمایشی'
                    mobile               = '09120000000'
                    isActive             = $isActive
                    isFirstLogin         = $isFirstLogin
                    nationalCode         = '0012345678'
                    userName             = "$pcode"
                    lastModificationDate = (Get-Date).ToUniversalTime().ToString('o')
                }
                $payload = $dto | ConvertTo-Json -Compress
                Write-Host "  [$stamp] ورود موفق برای $pcode (فعال=$isActive، ورود اول=$isFirstLogin) -> 200" -ForegroundColor Green
            }
        }

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
        $response.ContentLength64 = $bytes.Length
        $response.OutputStream.Write($bytes, 0, $bytes.Length)
        $response.OutputStream.Close()
    }
}
finally {
    $listener.Stop()
    $listener.Close()
    Write-Host ''
    Write-Host '  شبیه‌ساز متوقف شد.' -ForegroundColor DarkGray
}
