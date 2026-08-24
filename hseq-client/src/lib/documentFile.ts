// Turning a Bearer-protected API response into something the browser can show or
// save. The file routes need an Authorization header, so the bytes are fetched
// through documentApi and handed over as a blob URL - a direct link to the API
// would come back 401.

import { documentApi } from '../api/documentApi'

// Blob URLs hold their data until revoked. A tab opened with one still needs it
// after this function returns, so the revoke is deferred rather than immediate.
const REVOKE_DELAY_MS = 60_000

// Opens the document's file in a new tab for viewing. Returns false when the
// browser blocked the popup, so the caller can tell the user why nothing happened.
export async function viewDocumentFile(documentId: string): Promise<boolean> {
  // تب باید همین‌جا باز شود - پیش از هر await. مرورگر فقط پنجره‌ای را مجاز می‌داند که
  // در همان تپشِ کلیک کاربر ساخته شده باشد؛ باز کردنش پس از دریافت فایل، همیشه به
  // popup blocker می‌خورد.
  //
  // ‎'noopener'‎ هم عمداً حذف شد: طبق استاندارد، window.open با آن گزینه همیشه null
  // برمی‌گرداند، و کد این null را «بلاک شد» می‌خواند - علتِ اینکه هشدار حتی وقتی تب
  // درست باز می‌شد هم نمایش داده می‌شد. به‌جایش opener دستی پاک می‌شود که همان اثر
  // امنیتی را دارد.
  const tab = window.open('', '_blank')
  if (!tab) return false
  tab.opener = null

  // تا رسیدن فایل، تب سفیدِ خالی نگران‌کننده است؛ یک پیام کوتاه جایش می‌نشیند.
  tab.document.write(
    '<!doctype html><meta charset="utf-8"><title>در حال بارگذاری…</title>' +
      '<body style="font-family:sans-serif;direction:rtl;padding:24px">در حال بارگذاری فایل…</body>',
  )
  tab.document.close()

  try {
    const blob = await documentApi.getFile(documentId, false)
    const url = URL.createObjectURL(blob)
    tab.location.href = url
    window.setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS)
    return true
  } catch (error) {
    // تبِ بازشده بدون محتوا رها نشود؛ خطا به فراخوان می‌رسد تا پیام درست را نشان دهد.
    tab.close()
    throw error
  }
}

// Saves the document's file under its stored name (always "<DocumentNumber><ext>",
// so a downloaded revision is identifiable on disk).
export async function downloadDocumentFile(documentId: string, fileName: string): Promise<void> {
  const blob = await documentApi.getFile(documentId, true)
  const url = URL.createObjectURL(blob)

  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()

  window.setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS)
}
