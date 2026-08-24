// دکمه‌ی آیکونیِ «شیشه‌ای» (glass) که جای دکمه‌های متنیِ ستون عملیات را می‌گیرد.
// چون متن دکمه حذف شده، برچسب هم به‌عنوان راهنمای شناور (tooltip) و هم به‌عنوان
// aria-label استفاده می‌شود تا برای صفحه‌خوان‌ها معنا از دست نرود.

import type { ReactNode } from 'react'
import { SpinnerIcon } from './Icons'

// هر عملیات رنگ اختصاصی خودش را دارد تا در یک ردیفِ پر از آیکون، از روی رنگ هم
// قابل تشخیص باشد نه فقط از روی شکل. رنگ‌ها در index.css تعریف شده‌اند.
export type IconTone =
  | 'default'
  | 'view'
  | 'download'
  | 'history'
  | 'edit'
  | 'revise'
  | 'danger'
  // سه لحنِ افزوده برای پنل ادمین: تأیید/فعال‌سازی، هشدارِ غیرمخرب (تنزل نقش) و
  // عملیات خنثی مثل انصراف.
  | 'success'
  | 'warning'
  | 'muted'

type IconButtonProps = {
  label: string
  icon: ReactNode
  onClick: () => void
  disabled?: boolean
  // در حال انجام: آیکون جای خود را به نشانگر چرخان می‌دهد.
  pending?: boolean
  tone?: IconTone
}

export function IconButton({ label, icon, onClick, disabled, pending, tone = 'default' }: IconButtonProps) {
  return (
    <button
      type="button"
      className={`icon-btn${tone === 'default' ? '' : ` icon-btn--${tone}`}`}
      onClick={onClick}
      disabled={disabled || pending}
      aria-label={label}
    >
      <span className="icon-btn__glyph">{pending ? <SpinnerIcon /> : icon}</span>
      {/* راهنمای شناور: بالای دکمه باز می‌شود تا در جدولِ اسکرول‌دار بریده نشود. */}
      <span className="icon-btn__tip" role="tooltip">
        {label}
      </span>
    </button>
  )
}
