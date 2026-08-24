// دو حالتِ نمایشِ برنامه: روشن و تیره.
//
// مقدار انتخاب‌شده روی ‎<html data-theme>‎ می‌نشیند و همه‌ی رنگ‌ها در index.css از روی
// همین صفت عوض می‌شوند. همین منطق یک‌بار هم به‌صورت اسکریپت کوچک در index.html اجرا
// می‌شود تا صفحه پیش از اولین رنگ‌آمیزی تمِ درست را داشته باشد و پرشِ رنگ نبینیم.

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'hseq-theme'

// در حالت مرور ناشناس یا با کوکی‌های مسدود، دسترسی به localStorage استثنا می‌دهد؛
// نبودِ حافظه نباید برنامه را بخواباند، فقط انتخاب کاربر ماندگار نمی‌شود.
function readStored(): Theme | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY)
    return value === 'light' || value === 'dark' ? value : null
  } catch {
    return null
  }
}

function writeStored(theme: Theme): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, theme)
  } catch {
    /* بی‌اهمیت: فقط ماندگاری بین نشست‌ها از دست می‌رود. */
  }
}

export function getSystemTheme(): Theme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

// تمِ مؤثر: انتخاب صریح کاربر، و اگر نبود، ترجیح سیستم‌عامل.
export function resolveTheme(): Theme {
  return readStored() ?? getSystemTheme()
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
  writeStored(theme)
}
