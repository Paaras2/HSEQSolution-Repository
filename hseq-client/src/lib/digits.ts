// تبدیل ارقام بین لاتین و فارسی.
//
// قاعده‌ی پروژه: هر عددی که *نمایش* داده می‌شود فارسی است، و هر عددی که *پردازش* یا به
// سرور فرستاده می‌شود لاتین. برای همین هر ورودیِ کاربر پیش از مقایسه یا ارسال با
// toLatinDigits عادی‌سازی می‌شود - وگرنه کاربری که شماره‌ی سندِ نمایش‌داده‌شده را کپی و
// در جستجو پیست کند، هیچ نتیجه‌ای نمی‌گیرد.

const PERSIAN_DIGITS = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹']

// ارقام عربی (٠-٩) هم پوشش داده می‌شوند، چون بعضی صفحه‌کلیدها همان‌ها را تولید می‌کنند.
const ARABIC_ZERO = 0x0660
const PERSIAN_ZERO = 0x06f0

export function toPersianDigits(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return ''
  return String(value).replace(/[0-9]/g, (digit) => PERSIAN_DIGITS[Number(digit)])
}

export function toLatinDigits(value: string | null | undefined): string {
  if (!value) return ''
  return value.replace(/[۰-۹٠-٩]/g, (char) => {
    const code = char.charCodeAt(0)
    const base = code >= PERSIAN_ZERO ? PERSIAN_ZERO : ARABIC_ZERO
    return String(code - base)
  })
}
