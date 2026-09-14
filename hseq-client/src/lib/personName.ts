// نامِ نمایشیِ کاربر، از نام و نام خانوادگی‌ای که سامانه‌ی مدیریت کاربران برمی‌گرداند.

export interface PersonName {
  firstName?: string | null
  lastName?: string | null
}

function clean(value: string | null | undefined): string {
  return (value ?? '').replace(/\s+/g, ' ').trim()
}

/** «نام نام‌خانوادگی»، یا null وقتی هیچ‌کدام نیست - تا نمایش به کد پرسنلی برگردد. */
export function displayNameOf(name: PersonName): string | null {
  const full = [clean(name.firstName), clean(name.lastName)].filter(Boolean).join(' ')
  return full || null
}

/**
 * دو حرفِ آواتار: حرف اولِ نام و حرف اولِ نام خانوادگی. میانشان نیم‌فاصله می‌آید تا
 * حروف فارسی به هم نچسبند و مثل یک کلمه‌ی بی‌معنی خوانده نشوند.
 */
export function initialsOf(name: PersonName): string | null {
  const letters = [clean(name.firstName), clean(name.lastName)]
    .filter(Boolean)
    .map((part) => Array.from(part)[0])
  return letters.length > 0 ? letters.join('‌') : null
}
