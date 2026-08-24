import { toLatinDigits, toPersianDigits } from './digits'

// تبدیل تقویم جلالی (شمسی) و میلادی - بدون هیچ پکیج جانبی.
//
// قرارداد کل برنامه: هر چه به سرور می‌رود و از آن می‌آید میلادی و به شکل «yyyy-MM-dd»
// است؛ شمسی فقط لایه‌ی نمایش است. هیچ‌جا تاریخ شمسی ذخیره نمی‌شود.
//
// الگوریتم، پیاده‌سازی استاندارد و شناخته‌شده‌ی تبدیل جلالی است (مبنای محاسبه: شماره‌ی
// روز، نه شیء Date) تا تغییر ساعت تابستانی و منطقه‌ی زمانی روی نتیجه اثر نگذارد.

function div(a: number, b: number): number {
  return Math.trunc(a / b)
}

function mod(a: number, b: number): number {
  return a - Math.trunc(a / b) * b
}

// سال‌های شکست در چرخه‌ی ۳۳ساله‌ی تقویم جلالی - مبنای تشخیص سال کبیسه.
const BREAKS = [
  -61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181,
  1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178,
]

interface JalaliCalendarInfo {
  leap: number
  gy: number
  march: number
}

// محاسبه‌ی وضعیت کبیسه‌ی سال شمسی و روزِ مارسِ میلادی که اول فروردین روی آن می‌افتد.
function jalaliCalendar(jy: number, withoutLeap: boolean): JalaliCalendarInfo {
  const gy = jy + 621
  let leapJ = -14
  let jp = BREAKS[0]
  let jump = 0

  if (jy < jp || jy >= BREAKS[BREAKS.length - 1]) {
    throw new RangeError('سال شمسی خارج از محدوده‌ی پشتیبانی‌شده است: ' + jy)
  }

  for (let i = 1; i < BREAKS.length; i += 1) {
    const jm = BREAKS[i]
    jump = jm - jp
    if (jy < jm) break
    leapJ = leapJ + div(jump, 33) * 8 + div(mod(jump, 33), 4)
    jp = jm
  }

  let n = jy - jp
  leapJ = leapJ + div(n, 33) * 8 + div(mod(n, 33) + 3, 4)
  if (mod(jump, 33) === 4 && jump - n === 4) leapJ += 1

  const leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150
  const march = 20 + leapJ - leapG

  let leap = -1
  if (!withoutLeap) {
    if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33
    leap = mod(mod(n + 1, 33) - 1, 4)
    if (leap === -1) leap = 4
  }

  return { leap, gy, march }
}

// میلادی -> شماره‌ی روز
function gregorianToDayNumber(gy: number, gm: number, gd: number): number {
  let d =
    div((gy + div(gm - 8, 6) + 100100) * 1461, 4) +
    div(153 * mod(gm + 9, 12) + 2, 5) +
    gd -
    34840408
  d = d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752
  return d
}

// شماره‌ی روز -> میلادی
function dayNumberToGregorian(jdn: number): { gy: number; gm: number; gd: number } {
  let j = 4 * jdn + 139361631
  j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908
  const i = div(mod(j, 1461), 4) * 5 + 308
  const gd = div(mod(i, 153), 5) + 1
  const gm = mod(div(i, 153), 12) + 1
  const gy = div(j, 1461) - 100100 + div(8 - gm, 6)
  return { gy, gm, gd }
}

export interface JalaliDate {
  jy: number
  jm: number
  jd: number
}

export function jalaliToGregorian(jy: number, jm: number, jd: number): { gy: number; gm: number; gd: number } {
  const r = jalaliCalendar(jy, true)
  const jdn = gregorianToDayNumber(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1
  return dayNumberToGregorian(jdn)
}

export function gregorianToJalali(gy: number, gm: number, gd: number): JalaliDate {
  const jdn = gregorianToDayNumber(gy, gm, gd)
  const gregorianYear = dayNumberToGregorian(jdn).gy
  let jy = gregorianYear - 621
  const r = jalaliCalendar(jy, false)
  const firstFarvardin = gregorianToDayNumber(gregorianYear, 3, r.march)

  let k = jdn - firstFarvardin
  if (k >= 0) {
    if (k <= 185) {
      return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 }
    }
    k -= 186
  } else {
    jy -= 1
    k += 179
    if (r.leap === 1) k += 1
  }

  return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 }
}

export function isLeapJalaliYear(jy: number): boolean {
  return jalaliCalendar(jy, false).leap === 0
}

// طول ماه شمسی: ۶ ماه اول ۳۱، پنج ماه بعد ۳۰، اسفند ۲۹ (۳۰ در سال کبیسه).
export function jalaliMonthLength(jy: number, jm: number): number {
  if (jm <= 6) return 31
  if (jm <= 11) return 30
  return isLeapJalaliYear(jy) ? 30 : 29
}

export const JALALI_MONTH_NAMES = [
  'فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور',
  'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند',
]

// هفته‌ی ایرانی از شنبه شروع می‌شود؛ Date.getDay() یکشنبه را صفر می‌گیرد.
export const JALALI_WEEKDAY_SHORT = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج']

// نام کاملِ روزهای هفته، برای نمایش تاریخ در نوار بالا. ترتیبش با آرایه‌ی بالا یکی است.
export const JALALI_WEEKDAY_NAMES = [
  'شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه',
]

function pad2(n: number): string {
  return String(n).padStart(2, '0')
}

// «yyyy-MM-dd» میلادی -> «yyyy/MM/dd» شمسی با ارقام فارسی. ورودی نامعتبر رشته‌ی خالی
// می‌دهد. تنها نقطه‌ی تولیدِ متنِ تاریخ در برنامه است، پس فارسی‌سازی همین‌جا انجام می‌شود.
export function isoToJalaliText(iso: string): string {
  const parsed = parseIsoDate(iso)
  if (!parsed) return ''
  const j = gregorianToJalali(parsed.gy, parsed.gm, parsed.gd)
  return toPersianDigits(`${j.jy}/${pad2(j.jm)}/${pad2(j.jd)}`)
}

// «yyyy/MM/dd» یا «yyyy-MM-dd» شمسی -> «yyyy-MM-dd» میلادی. نامعتبر باشد null.
// ورودی می‌تواند ارقام فارسی یا عربی داشته باشد؛ همان چیزی که خودمان نمایش داده‌ایم و
// کاربر ممکن است کپی کند، باید دوباره قابل خواندن باشد.
export function jalaliTextToIso(text: string): string | null {
  const match = toLatinDigits(text).trim().replace(/[-.]/g, '/').match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})$/)
  if (!match) return null

  const jy = Number(match[1])
  const jm = Number(match[2])
  const jd = Number(match[3])

  if (jm < 1 || jm > 12) return null
  // روز بزرگ‌تر از طول ماه رد می‌شود تا «1405/12/30» در سال غیرکبیسه سُر نخورد به فروردین.
  if (jd < 1 || jd > jalaliMonthLength(jy, jm)) return null

  try {
    const g = jalaliToGregorian(jy, jm, jd)
    return `${g.gy}-${pad2(g.gm)}-${pad2(g.gd)}`
  } catch {
    return null
  }
}

export function jalaliToIso(jy: number, jm: number, jd: number): string {
  const g = jalaliToGregorian(jy, jm, jd)
  return `${g.gy}-${pad2(g.gm)}-${pad2(g.gd)}`
}

// فقط بخش تاریخِ یک رشته‌ی ISO خوانده می‌شود (نه new Date) تا منطقه‌ی زمانی روز را جابه‌جا نکند.
export function parseIsoDate(iso: string): { gy: number; gm: number; gd: number } | null {
  const match = (iso ?? '').slice(0, 10).match(/^(\d{4})-(\d{2})-(\d{2})$/)
  if (!match) return null
  return { gy: Number(match[1]), gm: Number(match[2]), gd: Number(match[3]) }
}

// شماره‌ی ستون در تقویم شمسی: شنبه = ۰ ... جمعه = ۶
export function jalaliWeekdayIndex(gy: number, gm: number, gd: number): number {
  const weekday = new Date(Date.UTC(gy, gm - 1, gd)).getUTCDay()
  return (weekday + 1) % 7
}

// امروز به شمسی - برای دکمه‌ی «امروز» و ماه پیش‌فرضِ باز شدن تقویم.
export function todayJalali(): JalaliDate {
  const now = new Date()
  return gregorianToJalali(now.getFullYear(), now.getMonth() + 1, now.getDate())
}
