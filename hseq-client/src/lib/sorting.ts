// مرتب‌سازی الفبایی فارسی برای فهرست‌های داده‌ی پایه.
//
// مقایسه‌ی ساده‌ی رشته‌ها (‎a < b‎) بر اساس کد یونیکد انجام می‌شود و برای فارسی نتیجه‌ی
// غلط می‌دهد؛ مثلاً «ی» عربی و «ی» فارسی دو کد متفاوت دارند و کنار هم نمی‌نشینند.
// Intl.Collator این‌ها را هم‌ارز می‌گیرد و ترتیب درست حروف را می‌داند - و چون در خودِ
// مرورگر هست، پکیجی هم لازم ندارد.
const collator = new Intl.Collator('fa', { numeric: true, sensitivity: 'base' })

// مقایسه‌گر عنوان، برای پاس دادن مستقیم به sort.
export function byTitle<T extends { title: string }>(a: T, b: T): number {
  return collator.compare(a.title, b.title)
}

// نسخه‌ی مرتب‌شده‌ی فهرست. عمداً کپی می‌گیرد تا آرایه‌ی داخل state دست‌نخورده بماند -
// sort آرایه‌ی اصلی را در جا عوض می‌کند و این در React منبع باگ‌های نامرئی است.
export function sortedByTitle<T extends { title: string }>(items: T[]): T[] {
  return [...items].sort(byTitle)
}
