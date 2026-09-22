import { toPersianDigits } from './digits'
import { isoToJalaliDateTimeText } from './jalali'

// ردِ حسابرسی (چه کسی، چه زمانی) در چند جا نمایش داده می‌شود - پیش‌نمایش سند و
// تاریخچه‌ی بازنگری - و منطقِ بازگشتی‌اش باید همه‌جا یکی باشد، وگرنه یک صفحه نام
// نشان می‌دهد و صفحه‌ی دیگر کد پرسنلی.

// «چه کسی»: نام کامل اگر سرور از سامانه‌ی مدیریت کاربران نگاشت کرده باشد، وگرنه
// کد پرسنلی - تا وقتی آن سامانه در دسترس نیست یا کد پیدا نمی‌شود، ردیف بی‌صاحب به
// نظر نرسد.
export function auditPersonText(pCode: number | null, name: string | null): string {
  if (name) return name
  if (pCode) return `کد پرسنلی ${toPersianDigits(pCode)}`
  return '—'
}

// «چه زمانی»: تاریخ شمسی + ساعتِ دقیق. نبودِ مقدار «—» می‌شود.
export function auditWhenText(iso: string | null): string {
  return iso ? isoToJalaliDateTimeText(iso) || '—' : '—'
}
