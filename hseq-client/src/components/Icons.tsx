// آیکون‌های خطی برنامه، به‌صورت SVG درون‌خطی نوشته شده‌اند تا هیچ پکیج آیکونی به
// پروژه اضافه نشود. همه از currentColor رنگ می‌گیرند، پس رنگ را دکمه‌ی میزبان
// تعیین می‌کند و در حالت هاور/غیرفعال خودبه‌خود هماهنگ می‌ماند.

import type { SVGProps } from 'react'

type IconProps = SVGProps<SVGSVGElement> & { size?: number }

// پوسته‌ی مشترک همه‌ی آیکون‌ها: اندازه، ضخامت خط و گِرد بودن سرِ خط‌ها یک‌جا تنظیم
// می‌شود تا آیکون‌ها کنار هم یکدست دیده شوند.
function Icon({ size = 16, children, ...rest }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...rest}
    >
      {children}
    </svg>
  )
}

// مشاهده‌ی فایل سند
export function EyeIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M2.5 12s3.5-6.5 9.5-6.5 9.5 6.5 9.5 6.5-3.5 6.5-9.5 6.5S2.5 12 2.5 12Z" />
      <circle cx="12" cy="12" r="2.75" />
    </Icon>
  )
}

// دانلود فایل سند
export function DownloadIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 3.5v11" />
      <path d="m7.5 10 4.5 4.5L16.5 10" />
      <path d="M4.5 16.5v2a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-2" />
    </Icon>
  )
}

// تاریخچه‌ی بازنگری‌ها
export function HistoryIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M3.5 9.5A9 9 0 1 1 3 12" />
      <path d="M3.2 4.5v5h5" />
      <path d="M12 7.5V12l3 1.8" />
    </Icon>
  )
}

// ویرایش اطلاعات سند
export function EditIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M4 20h4.2l9.4-9.4a2.1 2.1 0 0 0 0-3l-1.2-1.2a2.1 2.1 0 0 0-3 0L4 15.8V20Z" />
      <path d="m14.6 6.4 3 3" />
    </Icon>
  )
}

// صدور بازنگری جدید
export function ReviseIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M20 12a8 8 0 1 1-2.4-5.7" />
      <path d="M20.5 3.5V8H16" />
    </Icon>
  )
}

// غیرفعال کردن سند (حذف نرم)
export function DeactivateIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="m6.6 6.6 10.8 10.8" />
    </Icon>
  )
}

// بستن / پاک کردن انتخاب
export function CloseIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M6 6 18 18M18 6 6 18" />
    </Icon>
  )
}

/* ---------------------------------------------------------------------------
   آیکون‌های ناوبری و نوار بالا
   --------------------------------------------------------------------------- */

// اسناد
export function FileTextIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z" />
      <path d="M14 3v5h5" />
      <path d="M9 13h6M9 17h4" />
    </Icon>
  )
}

// جستجو (هم در منو و هم داخل کادر جستجوی نوار بالا)
export function SearchIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="11" cy="11" r="6.5" />
      <path d="m16 16 4.5 4.5" />
    </Icon>
  )
}

// فیلترهای جستجو (قیف)
export function FilterIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M3.5 5h17l-6.6 7.6v5.6l-3.8 2.3v-7.9L3.5 5Z" />
    </Icon>
  )
}

// داشبورد
export function DashboardIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="3.5" y="3.5" width="7" height="7" rx="1.6" />
      <rect x="13.5" y="3.5" width="7" height="4.5" rx="1.6" />
      <rect x="3.5" y="13.5" width="7" height="7" rx="1.6" />
      <rect x="13.5" y="11" width="7" height="9.5" rx="1.6" />
    </Icon>
  )
}

// پنل ادمین
export function AdminIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 3.2 4.8 6v5.4c0 4.3 3 8.2 7.2 9.4 4.2-1.2 7.2-5.1 7.2-9.4V6Z" />
      <path d="m9.2 12 2 2 3.6-3.8" />
    </Icon>
  )
}

// تقویم - کنار تاریخ روز در نوار بالا
export function CalendarIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="3.5" y="5" width="17" height="15.5" rx="2.4" />
      <path d="M3.5 9.8h17M8.2 3.2v3.4M15.8 3.2v3.4" />
    </Icon>
  )
}

// حالت روشن
export function SunIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2.6v2.2M12 19.2v2.2M2.6 12h2.2M19.2 12h2.2M5.4 5.4l1.6 1.6M17 17l1.6 1.6M18.6 5.4 17 7M7 17l-1.6 1.6" />
    </Icon>
  )
}

// حالت تیره
export function MoonIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M20.5 14.2A8.6 8.6 0 0 1 9.8 3.5a8.7 8.7 0 1 0 10.7 10.7Z" />
    </Icon>
  )
}

// خروج از حساب
export function LogoutIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M14.5 4.5h3a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2h-3" />
      <path d="M10 8 6 12l4 4" />
      <path d="M6 12h8.5" />
    </Icon>
  )
}

// جمع/باز کردن منو (« و ») - جهتش را CSS در حالت جمع‌شده برمی‌گرداند
export function ChevronsIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="m11 6-6 6 6 6M18 6l-6 6 6 6" />
    </Icon>
  )
}

// دکمه‌ی منوی موبایل
export function MenuIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </Icon>
  )
}

// فعال کردن دوباره‌ی یک ردیفِ غیرفعال (کلید روشن/خاموش)
export function PowerIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 3.5v8" />
      <path d="M17.5 6.8a7.5 7.5 0 1 1-11 0" />
    </Icon>
  )
}

// ارتقای سطح دسترسی
export function ArrowUpIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 20V4.5" />
      <path d="m5.5 11 6.5-6.5 6.5 6.5" />
    </Icon>
  )
}

// تنزل سطح دسترسی
export function ArrowDownIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 4v15.5" />
      <path d="m18.5 13-6.5 6.5L5.5 13" />
    </Icon>
  )
}

// برداشتن دسترسی کاربر
export function UserMinusIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="9.5" cy="8" r="3.75" />
      <path d="M3 20a6.5 6.5 0 0 1 13 0" />
      <path d="M17 13.5h4.5" />
    </Icon>
  )
}

// افزودن مورد جدید
export function PlusIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 5v14M5 12h14" />
    </Icon>
  )
}

// تب‌های پنل ادمین ------------------------------------------------------------
// پروژه
export function BriefcaseIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="3" y="7.5" width="18" height="12" rx="2" />
      <path d="M9 7.5V6a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v1.5" />
      <path d="M3 12.5h18" />
    </Icon>
  )
}

// مدیریت سازمانی
export function BuildingIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M4 20V5.5a1.5 1.5 0 0 1 1.5-1.5h7A1.5 1.5 0 0 1 14 5.5V20" />
      <path d="M14 10h4.5A1.5 1.5 0 0 1 20 11.5V20" />
      <path d="M2.5 20h19" />
      <path d="M7 8h4M7 12h4M7 16h4" />
    </Icon>
  )
}

// فعالیت سازمانی
export function LayersIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="m12 3.5 8.5 4.5-8.5 4.5L3.5 8 12 3.5Z" />
      <path d="m3.5 12.5 8.5 4.5 8.5-4.5" />
      <path d="m3.5 16.5 8.5 4 8.5-4" />
    </Icon>
  )
}

// آرشیو شماره‌های قدیمی
export function ArchiveIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="3" y="3.5" width="18" height="4.5" rx="1.4" />
      <path d="M4.8 8v10a2 2 0 0 0 2 2h10.4a2 2 0 0 0 2-2V8" />
      <path d="M10 12h4" />
    </Icon>
  )
}

// کاربران و دسترسی
export function UsersIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="9" cy="8" r="3.5" />
      <path d="M2.5 19.5a6.5 6.5 0 0 1 13 0" />
      <path d="M16 5a3.5 3.5 0 0 1 0 6.5" />
      <path d="M17.5 14.5a5.5 5.5 0 0 1 4 5" />
    </Icon>
  )
}

// هشدار - کارت‌های موعد بازبینی و تب هشدارهای داشبورد
export function AlertTriangleIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 4.2 2.8 19.3h18.4L12 4.2Z" />
      <path d="M12 10v4" />
      <path d="M12 17h.01" />
    </Icon>
  )
}

// نمودار - تب تفکیک‌های داشبورد
export function ChartIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />
    </Icon>
  )
}

// نشانگر چرخان برای عملیات در حال انجام
export function SpinnerIcon({ size = 16, ...rest }: IconProps) {
  return (
    <Icon size={size} className="icon-spin" {...rest}>
      <path d="M12 3.5a8.5 8.5 0 1 0 8.5 8.5" />
    </Icon>
  )
}

/* ---------------------------------------------------------------------------
   آیکون‌های فرم افزودن سند
   --------------------------------------------------------------------------- */

// بارگذاری فایل - داخل کادر «فایل را رها کنید»
export function UploadIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M12 16V4.5" />
      <path d="m7.5 9 4.5-4.5L16.5 9" />
      <path d="M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3" />
    </Icon>
  )
}

// مدارک مرتبط - سرِ بخشِ جمع‌شونده
export function LinkIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M10 13.5a3.5 3.5 0 0 0 5 0l3-3a3.5 3.5 0 0 0-5-5l-1.4 1.4" />
      <path d="M14 10.5a3.5 3.5 0 0 0-5 0l-3 3a3.5 3.5 0 0 0 5 5l1.4-1.4" />
    </Icon>
  )
}

// فلش باز/بسته شدن بخش جمع‌شونده - چرخشش را CSS انجام می‌دهد
export function ChevronDownIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="m6 9.5 6 6 6-6" />
    </Icon>
  )
}

// تیکِ «تکمیل شد» - کنار موارد الزامیِ پرشده
export function CheckIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="m5 12.5 4.5 4.5L19 7" />
    </Icon>
  )
}
