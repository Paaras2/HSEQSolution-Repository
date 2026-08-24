// قرارداد مشترکِ فیلترهای جستجو بین نوار جستجوی بالای صفحه و فهرست اسناد.
//
// مبدأ حقیقت، پارامترهای آدرس است نه state داخلی: با این کار دکمه‌ی بازگشت مرورگر،
// تازه‌سازی صفحه و اشتراک‌گذاری لینکِ یک جستجو بدون کد اضافه کار می‌کنند. این فایل جدا
// از کامپوننت است تا هر دو طرف بدون وابستگی به هم از یک تعریف استفاده کنند.

import { toLatinDigits } from './digits'

// نام پارامترها یک‌جا تعریف شده تا تغییرشان جای دیگری جا نماند.
export const SEARCH_PARAM = {
  query: 'q',
  management: 'mgmt',
  activity: 'act',
  onlyLatest: 'latest',
  inFileContent: 'content',
} as const

export interface SearchFilters {
  query: string
  managementId: string
  activityId: string
  onlyLatest: boolean
  inFileContent: boolean
}

export const EMPTY_FILTERS: SearchFilters = {
  query: '',
  managementId: '',
  activityId: '',
  onlyLatest: false,
  inFileContent: false,
}

export function readFilters(params: URLSearchParams): SearchFilters {
  return {
    query: params.get(SEARCH_PARAM.query) ?? '',
    managementId: params.get(SEARCH_PARAM.management) ?? '',
    activityId: params.get(SEARCH_PARAM.activity) ?? '',
    onlyLatest: params.get(SEARCH_PARAM.onlyLatest) === '1',
    inFileContent: params.get(SEARCH_PARAM.inFileContent) === '1',
  }
}

// آیا اصلاً جستجویی در کار است؟ فهرست اسناد با همین تصمیم می‌گیرد که عنوانِ «همه‌ی
// اسناد» را نشان دهد یا «نتیجه‌ی جستجو» را، و تراشه‌های فیلتر را رندر کند یا نه.
export function hasAnyFilter(filters: SearchFilters): boolean {
  return (
    filters.query.trim() !== '' ||
    filters.managementId !== '' ||
    filters.activityId !== '' ||
    filters.onlyLatest ||
    filters.inFileContent
  )
}

// فقط فیلترهای داخل پنل شمرده می‌شوند، نه خودِ عبارت جستجو - عبارت که در کادر پیداست و
// شمردنش روی نشانِ دکمه‌ی فیلتر گمراه‌کننده است.
export function countPanelFilters(filters: SearchFilters): number {
  return (
    (filters.managementId ? 1 : 0) +
    (filters.activityId ? 1 : 0) +
    (filters.onlyLatest ? 1 : 0) +
    (filters.inFileContent ? 1 : 0)
  )
}

// مقادیر خالی اصلاً نوشته نمی‌شوند تا آدرسِ یک جستجوی ساده تمیز بماند.
export function filtersToParams(filters: SearchFilters): URLSearchParams {
  const params = new URLSearchParams()
  // ارقام فارسیِ تایپ‌شده به لاتین برمی‌گردند، چون داده‌ی سمت سرور لاتین است.
  const query = toLatinDigits(filters.query).trim()
  if (query) params.set(SEARCH_PARAM.query, query)
  if (filters.managementId) params.set(SEARCH_PARAM.management, filters.managementId)
  if (filters.activityId) params.set(SEARCH_PARAM.activity, filters.activityId)
  if (filters.onlyLatest) params.set(SEARCH_PARAM.onlyLatest, '1')
  if (filters.inFileContent) params.set(SEARCH_PARAM.inFileContent, '1')
  return params
}
