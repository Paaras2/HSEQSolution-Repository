<#
.SYNOPSIS
    ورود به سامانه را مستقیماً می‌سنجد، بدون مرورگر و بدون IIS.

.DESCRIPTION
    وقتی ورود کار نمی‌کند، سه چیزِ کاملاً جدا می‌توانند مقصر باشند: پیکربندی IIS،
    دسترسی به دیتابیس سامانه‌ی مدیریت کاربران، یا قالبِ ذخیره‌شده‌ی رمز. از راه
    مرورگر هر سه به یک شکل دیده می‌شوند - یک پیام خطا - و عیب‌یابی به حدس‌زدن
    تبدیل می‌شود.

    این اسکریپت IIS را کنار می‌گذارد و همان کدی را اجرا می‌کند که هنگام ورودِ واقعی
    اجرا می‌شود، با همان appsettings و همان رشته‌ی اتصال. پس اگر اینجا کار کند و در
    مرورگر نه، مشکل از IIS است؛ و اگر اینجا هم کار نکند، پیامش می‌گوید کدام یک از
    آن دوِ دیگر.

    رمز روی صفحه echo نمی‌شود و هیچ‌جا لاگ یا ذخیره نمی‌شود.

.PARAMETER SitePath
    پوشه‌ای که سامانه در آن مستقر شده است.

.PARAMETER Username
    کد پرسنلی‌ای که می‌خواهید ورودش را بسنجید. اگر ندهید، فقط دسترسی به دیتابیس و
    شمارش قالب‌های رمز گزارش می‌شود - که خودش برای پیدا کردن مشکل دسترسی کافی است.

.EXAMPLE
    .\Test-UmLogin.ps1
    فقط دیتابیس را وارسی می‌کند: دسترسی، جدول، ستون‌ها، و چند کاربر در هر قالب.

.EXAMPLE
    .\Test-UmLogin.ps1 -Username 3256
    ورود این حساب را می‌سنجد؛ رمز را از شما می‌پرسد.
#>
[CmdletBinding()]
param(
    [string]$SitePath = 'D:\HouzoriApps\HSEQTest',
    [string]$Username
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $SitePath 'HSEQ.API.exe'
if (-not (Test-Path $exe)) {
    Write-Host "HSEQ.API.exe در «$SitePath» نیست." -ForegroundColor Red
    Write-Host 'مسیر استقرار را با ‎-SitePath‎ بدهید.' -ForegroundColor Yellow
    exit 2
}

$settings = Join-Path $SitePath 'appsettings.Production.json'
if (-not (Test-Path $settings)) {
    Write-Host "appsettings.Production.json در «$SitePath» نیست." -ForegroundColor Red
    exit 2
}

# همان محیطی که ANCM هنگام اجرای واقعی می‌سازد، وگرنه appsettings.Production.json
# اصلاً خوانده نمی‌شود و این سنجش چیز دیگری را می‌سنجد.
$previous = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$here = Get-Location
try {
    Set-Location $SitePath
    if ($Username) {
        & $exe --check-um-login $Username
    } else {
        & $exe --check-um
    }
    $code = $LASTEXITCODE
}
finally {
    Set-Location $here
    $env:ASPNETCORE_ENVIRONMENT = $previous
}

Write-Host ''
switch ($code) {
    0 {
        if ($Username) { Write-Host 'ورود پذیرفته شد - مسیر احراز هویت سالم است.' -ForegroundColor Green }
        else { Write-Host 'دیتابیس سامانه‌ی مدیریت کاربران در دسترس است.' -ForegroundColor Green }
    }
    1 {
        Write-Host 'ورود پذیرفته نشد. خط‌های بالا می‌گویند چرا.' -ForegroundColor Yellow
    }
    2 {
        Write-Host 'بدون ‎-Username‎ فقط دیتابیس UM وارسی می‌شود، و تنظیمات روی حالت Database نیست.' -ForegroundColor Yellow
        Write-Host 'در حالت Api ورود یک حساب را بسنجید:  .\Test-UmLogin.ps1 -Username <کد پرسنلی>' -ForegroundColor Yellow
    }
    default {
        Write-Host "برنامه با کد $code خارج شد." -ForegroundColor Red
    }
}

exit $code
