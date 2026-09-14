// شناسه‌ی buildی که سرو می‌شود.
//
// Publish-Production.ps1 هنگام ساخت کلاینت آن را در VITE_RELEASE_ID می‌گذارد و در پای
// صفحه‌ی ورود و منو دیده می‌شود. چرا: بعد از هر استقرار، تنها راهِ فهمیدنِ اینکه سرور
// واقعاً کدام build را سرو می‌کند نگاه کردن به نام فایل‌های assets در کنسول مرورگر بود -
// و یک بار همین باعث شد خطای یک build قدیمی به build تازه نسبت داده شود.
//
// در محیط توسعه مقدار ندارد و چیزی نمایش داده نمی‌شود.

import { toPersianDigits } from './digits'

const releaseId = import.meta.env.VITE_RELEASE_ID?.trim()

/** «HSEQ_20260914-100820» ← «۲۰۲۶۰۹۱۴-۱۰۰۸۲۰»، یا null در محیط توسعه. */
export const releaseLabel: string | null = releaseId ? toPersianDigits(releaseId.replace(/^HSEQ_/, '')) : null
