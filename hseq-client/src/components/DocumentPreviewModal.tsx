import { useEffect, useState } from 'react'
import { Modal } from './Modal'
import { documentApi } from '../api/documentApi'
import { downloadDocumentFile } from '../lib/documentFile'
import { ApiError } from '../lib/httpClient'
import type { DocumentDto } from '../types/api'
import { LoadingState, ErrorState } from './StateViews'

interface DocumentPreviewModalProps {
  document: DocumentDto
  onClose: () => void
}

// فقط PDF و تصویر مستقیم در مرورگر پیش‌نمایش می‌شوند؛ بقیه‌ی فرمت‌ها (Word، اکسل و...)
// راهی برای نمایش درون صفحه ندارند، پس فقط گزینه‌ی دانلود نشان داده می‌شود.
function isPreviewableType(mimeType: string): 'pdf' | 'image' | null {
  if (mimeType === 'application/pdf') return 'pdf'
  if (mimeType.startsWith('image/')) return 'image'
  return null
}

// پیش‌نمایش سریع فایل مدرک، بدون ترک صفحه‌ی نتایج جستجو - «هدایت به اقدام بعدی» یعنی
// کاربر با یک کلیک ببیند این همان مدرکیه که دنبالش بوده، بدون دانلود کامل فایل.
export function DocumentPreviewModal({ document, onClose }: DocumentPreviewModalProps) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null)
  const [previewKind, setPreviewKind] = useState<'pdf' | 'image' | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    let createdUrl: string | null = null

    setIsLoading(true)
    setError(null)

    documentApi
      .getFile(document.key, false)
      .then((blob) => {
        if (cancelled) return
        createdUrl = URL.createObjectURL(blob)
        setObjectUrl(createdUrl)
        setPreviewKind(isPreviewableType(blob.type))
      })
      .catch((err) => {
        if (cancelled) return
        setError(err instanceof ApiError ? err.message : 'امکان بارگذاری فایل برای پیش‌نمایش وجود ندارد.')
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })

    return () => {
      cancelled = true
      if (createdUrl) URL.revokeObjectURL(createdUrl)
    }
  }, [document.key])

  return (
    <Modal title={`پیش‌نمایش ${document.number}`} subtitle={document.name} size="lg" onClose={onClose}>
      <div className="preview-body">
        {isLoading && <LoadingState title="در حال بارگذاری فایل..." />}

        {!isLoading && error && (
          <ErrorState
            title={error}
            action={
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => downloadDocumentFile(document.key, document.fileName ?? document.number)}
              >
                دانلود فایل
              </button>
            }
          />
        )}

        {!isLoading && !error && objectUrl && previewKind === 'pdf' && (
          <iframe src={objectUrl} title={document.number} className="preview-frame" />
        )}

        {!isLoading && !error && objectUrl && previewKind === 'image' && (
          <img src={objectUrl} alt={document.name} className="preview-image" />
        )}

        {!isLoading && !error && objectUrl && !previewKind && (
          <div className="state-panel">
            <strong>پیش‌نمایش این نوع فایل پشتیبانی نمی‌شود.</strong>
            <span className="text-muted">فایل ({document.fileName}) را دانلود کنید.</span>
            <button
              type="button"
              className="btn btn-primary btn-sm"
              onClick={() => downloadDocumentFile(document.key, document.fileName ?? document.number)}
            >
              دانلود فایل
            </button>
          </div>
        )}
      </div>
    </Modal>
  )
}
