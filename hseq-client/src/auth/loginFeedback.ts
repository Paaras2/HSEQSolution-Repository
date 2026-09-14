// منطقِ صفحه‌ی ورود، بدون رابط کاربری: عادی‌سازی ورودی، اعتبارسنجی، و ترجمه‌ی پاسخ
// سرور به پیامی که کاربر می‌بیند. از کامپوننت جداست تا بدون مرورگر آزموده شود.

import { toLatinDigits } from '../lib/digits.ts'

// ---------------------------------------------------------------------------
// ورودی
// ---------------------------------------------------------------------------

// نویسه‌های نامرئی‌ای که متنِ کپی‌شده از یک سند فارسی با خود می‌آورد: نیم‌فاصله،
// نشانه‌های جهت (LRM/RLM)، و محصورکننده‌های جهت. هیچ‌کدام بخشی از کد پرسنلی نیستند.
const INVISIBLE_MARKS = /[\s\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]/g

/**
 * کد پرسنلی را همان‌طور که سامانه‌ی مدیریت کاربران انتظار دارد درمی‌آورد: ارقام لاتین،
 * بدون فاصله و نویسه‌ی نامرئی. رمز عبور هرگز از این مسیر نمی‌گذرد.
 */
export function normalizeUsername(raw: string): string {
  return toLatinDigits(raw).replace(INVISIBLE_MARKS, '')
}

export interface LoginFieldErrors {
  username?: string
  password?: string
}

export function validateLogin(username: string, password: string): LoginFieldErrors {
  const errors: LoginFieldErrors = {}
  if (!username) errors.username = 'کد پرسنلی را وارد کنید.'
  if (!password) errors.password = 'رمز عبور را وارد کنید.'
  return errors
}

/**
 * آیا متن حرف یا رقمِ فارسی/عربی دارد؟ رمزها تقریباً همیشه لاتین‌اند، پس این معمولاً
 * یعنی صفحه‌کلید روی فارسی مانده - رایج‌ترین دلیلِ «رمز درست است ولی وارد نمی‌شوم».
 * فقط هشدار می‌دهد؛ رمز را تغییر نمی‌دهد.
 */
export function looksLikePersianKeyboard(value: string): boolean {
  return /[\u0600-\u06FF]/.test(value)
}

// اگر پاسخ ورود بیش از این طول بکشد، کاربر باید بداند صفحه قفل نشده است. سرور تا ۱۰
// ثانیه منتظر سامانه‌ی کاربران می‌ماند.
export const SLOW_LOGIN_HINT_MS = 4000

// ---------------------------------------------------------------------------
// پاسخ سرور
// ---------------------------------------------------------------------------

export type LoginFailureKind =
  | 'invalid_credentials'
  | 'inactive'
  | 'unavailable'
  | 'missing_fields'
  | 'server_error'
  | 'network'
  | 'unknown'

export interface LoginFailure {
  kind: LoginFailureKind
  tone: 'danger' | 'warning'
  title: string
  detail: string
  /** خطا از سمت کاربر نیست و تلاش دوباره معنی دارد. */
  canRetry: boolean
}

// شکلِ ApiError، بدون وابستگی به httpClient - که در Node بارگذاری نمی‌شود.
interface ErrorLike {
  status?: unknown
  code?: unknown
  reason?: unknown
  message?: unknown
}

function classify(reason: string | undefined, status: number | undefined, code: number | undefined): LoginFailureKind {
  if (
    reason === 'invalid_credentials' ||
    reason === 'inactive' ||
    reason === 'unavailable' ||
    reason === 'missing_fields' ||
    reason === 'server_error'
  ) {
    return reason
  }

  // بدون «reason»: سروری قدیمی‌تر، یا خطایی که اصلاً به اکشن ورود نرسید (IIS، استثنای
  // مهارنشده). وضعیتِ ۵xxِ خودِ HTTP یعنی خرابی در همین سرور است؛ نسبت دادنش به
  // سامانه‌ی کاربران عیب‌یابی را به جای اشتباه می‌فرستاد. فقط کدِ ۵xx داخل بدنه‌ی یک
  // پاسخ ۴۰۰ (سرورِ پیش از reason) یعنی سامانه‌ی کاربران در دسترس نبوده است.
  if (status === 403) return 'inactive'
  if (status !== undefined && status >= 500) return 'server_error'
  if (code !== undefined && code >= 500) return 'unavailable'
  if (status === 400 || status === 401) return 'invalid_credentials'
  return 'unknown'
}

export function describeLoginFailure(error: unknown): LoginFailure {
  // fetch وقتی هیچ پاسخی نمی‌رسد TypeError می‌دهد: سرور خاموش، شبکه قطع.
  if (error instanceof TypeError) {
    return {
      kind: 'network',
      tone: 'warning',
      title: 'اتصال به سامانه برقرار نشد',
      detail: 'اتصال شبکه‌ی خود را بررسی کنید و دوباره تلاش کنید.',
      canRetry: true,
    }
  }

  const e = (typeof error === 'object' && error !== null ? error : {}) as ErrorLike
  const reason = typeof e.reason === 'string' ? e.reason : undefined
  const status = typeof e.status === 'number' ? e.status : undefined
  const code = typeof e.code === 'number' ? e.code : undefined
  const message = typeof e.message === 'string' && e.message.trim() ? e.message.trim() : undefined

  switch (classify(reason, status, code)) {
    case 'invalid_credentials':
      // پیامِ خودِ سامانه‌ی کاربران اولویت دارد: ممکن است چیزی جز «رمز غلط» بگوید،
      // مثلاً قفل شدنِ حساب.
      return {
        kind: 'invalid_credentials',
        tone: 'danger',
        title: message ?? 'کد پرسنلی یا رمز عبور درست نیست',
        detail: 'همان کد پرسنلی و رمزی را وارد کنید که در سامانه‌ی مدیریت کاربران دارید. به زبان صفحه‌کلید و Caps Lock دقت کنید.',
        canRetry: false,
      }

    case 'inactive':
      return {
        kind: 'inactive',
        tone: 'danger',
        title: 'حساب کاربری شما غیرفعال است',
        detail: 'رمز عبور درست بود، اما حساب شما در سامانه‌ی مدیریت کاربران فعال نیست. برای فعال‌سازی با واحد فناوری اطلاعات تماس بگیرید.',
        canRetry: false,
      }

    case 'unavailable':
      return {
        kind: 'unavailable',
        tone: 'warning',
        title: 'سامانه‌ی مدیریت کاربران پاسخ نمی‌دهد',
        detail: 'مشکل از کد پرسنلی یا رمز شما نیست. چند لحظه بعد دوباره تلاش کنید؛ اگر ادامه داشت با پشتیبانی تماس بگیرید.',
        canRetry: true,
      }

    case 'server_error':
      return {
        kind: 'server_error',
        tone: 'warning',
        title: 'خطای داخلی سامانه',
        detail: 'ورود به دلیل خطایی در سرورِ همین سامانه انجام نشد. مشکل از کد پرسنلی یا رمز شما نیست؛ لطفاً به پشتیبانی اطلاع دهید.',
        canRetry: true,
      }

    case 'missing_fields':
      return {
        kind: 'missing_fields',
        tone: 'danger',
        title: message ?? 'کد پرسنلی و رمز عبور را وارد کنید',
        detail: '',
        canRetry: false,
      }

    default:
      return {
        kind: 'unknown',
        tone: 'danger',
        title: 'ورود انجام نشد',
        detail: message ?? 'لطفاً دوباره تلاش کنید.',
        canRetry: true,
      }
  }
}
