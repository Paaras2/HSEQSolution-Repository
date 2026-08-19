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
  const blob = await documentApi.getFile(documentId, false)
  const url = URL.createObjectURL(blob)

  const opened = window.open(url, '_blank', 'noopener')
  if (!opened) {
    URL.revokeObjectURL(url)
    return false
  }

  window.setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS)
  return true
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
