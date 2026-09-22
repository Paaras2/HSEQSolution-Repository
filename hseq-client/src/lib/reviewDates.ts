// اعتبارسنجی سمت کلاینتِ همان قاعده‌ای که سرور اجرا می‌کند
// (HSEQ.Service.Services.Services.DocumentService.EnsureReviewDateOrder):
// وقتی هر دو تاریخ پر شده‌اند، «بازبینی فعلی» باید واقعاً جلوتر از «بازبینی قبلی»
// باشد. مقادیر به شکل ISO «yyyy-MM-dd» هستند، پس مقایسه‌ی رشته‌ای همان ترتیب
// تاریخی را می‌دهد - نیازی به ساختن شیء Date نیست.
export function reviewDateOrderError(formerReviewDate: string, currentReviewDate: string): string | null {
  if (!formerReviewDate || !currentReviewDate) return null
  if (currentReviewDate <= formerReviewDate) return 'تاریخ بازبینی فعلی باید از تاریخ بازبینی قبلی جلوتر باشد.'
  return null
}
