<#
.SYNOPSIS
    شبیه‌ساز محلی سرویس User Management - فقط برای آزمایش، هرگز روی سرور عملیاتی.

.DESCRIPTION
    وقتی ماشین به شبکه‌ی سازمان وصل نیست، سرویس واقعی UM
    (http://172.17.0.254:8030/api/) در دسترس نیست و هیچ‌کس نمی‌تواند وارد شود -
    یعنی صفحه‌ی ورود و کل جریان احراز هویت قابل آزمایش نیست.

    این اسکریپت همان یک اندپوینتی را که HSEQ.API واقعاً صدا می‌زند شبیه‌سازی می‌کند:

        POST {UmUrl}Auth/checkCredential   →  CheckCredentialDto

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

.EXAMPLE
    .\Start-UmSimulator.ps1
    سپس در appsettings.Production.json روی همین ماشین:
        "UserManagementAPI": { "Url": "http://localhost:8899/api/" }

.NOTES
    ⚠ این سرویس هر رمزی را می‌پذیرد. تنها محافظش این است که فقط روی loopback
    گوش می‌دهد و از بیرونِ ماشین اصلاً قابل دسترسی نیست.

    هرگز نباید در بسته‌ی انتشار برود یا روی سرور عملیاتی اجرا شود. اگر تنظیمات
    عملیاتی به این شبیه‌ساز اشاره کند، عملاً احراز هویت سامانه دور زده شده است.
#>
[CmdletBinding()]
param(
    [int]$Port = 8899,
    [string]$Hostname = 'localhost',
    [int[]]$AllowedPcodes = @(3256, 3548)
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

Write-Host ''
Write-Host '  شبیه‌ساز سرویس UM - فقط آزمایشی' -ForegroundColor Yellow
Write-Host '  ────────────────────────────────────────────' -ForegroundColor DarkGray
Write-Host "  نشانی      : $prefix" -ForegroundColor White
Write-Host "  اندپوینت   : POST ${prefix}api/Auth/checkCredential"
Write-Host "  کدهای مجاز : $($AllowedPcodes -join ', ')"
Write-Host '  رمز عبور   : هر مقدار غیرخالی پذیرفته می‌شود' -ForegroundColor Yellow
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
            $password = $null
            try {
                $parsed = $body | ConvertFrom-Json
                # نام فیلدها همان LoginRequestModel است؛ تطبیق بدون حساسیت به بزرگی حروف.
                foreach ($p in $parsed.PSObject.Properties) {
                    if ($p.Name -ieq 'Username') { $username = [string]$p.Value }
                    if ($p.Name -ieq 'Password') { $password = [string]$p.Value }
                }
            } catch {
                $username = $null
            }

            $pcode = 0
            $isNumeric = [int]::TryParse(($username ?? ''), [ref]$pcode)

            if (-not $isNumeric -or $AllowedPcodes -notcontains $pcode -or [string]::IsNullOrWhiteSpace($password)) {
                # همان شکلی که UmService انتظار دارد: کد غیر ۲xx با بدنه‌ی ErrorDto.
                $response.StatusCode = 401
                $payload = '{"message":"نام کاربری یا رمز عبور نامعتبر است","code":401}'
                Write-Host "  [$stamp] ورود ناموفق برای '$username' -> 401" -ForegroundColor Yellow
            }
            else {
                $response.StatusCode = 200
                # فقط PCode و IsActive توسط HSEQ.API خوانده می‌شوند؛ بقیه برای کامل
                # بودن شکل پاسخ است.
                $dto = [ordered]@{
                    pCode                = $pcode
                    firstName            = 'کاربر'
                    lastName             = 'آزمایشی'
                    mobile               = ''
                    isActive             = $true
                    isFirstLogin         = $false
                    nationalCode         = ''
                    userName             = "$pcode"
                    lastModificationDate = $null
                }
                $payload = $dto | ConvertTo-Json -Compress
                Write-Host "  [$stamp] ورود موفق برای $pcode -> 200" -ForegroundColor Green
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
