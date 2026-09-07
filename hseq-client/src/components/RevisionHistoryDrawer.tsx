import { useCallback, useEffect, useState } from 'react'
import { documentApi } from '../api/documentApi'
import { downloadDocumentFile, viewDocumentFile } from '../lib/documentFile'
import { ApiError } from '../lib/httpClient'
import { documentVersionLabel } from '../types/api'
import { isoToJalaliText } from '../lib/jalali'
import type { DocumentDto } from '../types/api'
import { Modal } from './Modal'
import { LoadingState, ErrorState } from './StateViews'
import { toPersianDigits } from '../lib/digits'

interface RevisionHistoryDrawerProps {
  document: DocumentDto
  onClose: () => void
}

function revisionLabel(doc: DocumentDto): string {
  return (
    documentVersionLabel(doc.lastVersion) +
    (doc.contentRevision ? toPersianDigits(String(doc.contentRevision).padStart(2, '0')) : '')
  )
}

// Read-only view of one document's full revision chain. Each revision keeps its
// own file, so every entry here is independently viewable - that is the whole
// point of retaining superseded revisions rather than overwriting them.
export function RevisionHistoryDrawer({ document, onClose }: RevisionHistoryDrawerProps) {
  const [revisions, setRevisions] = useState<DocumentDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [pendingFileId, setPendingFileId] = useState<string | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)

  const loadHistory = useCallback(() => {
    setIsLoading(true)
    setLoadError(null)
    documentApi
      .getRevisionHistory(document.key)
      .then(setRevisions)
      .catch((err) =>
        setLoadError(
          err instanceof ApiError ? err.message : 'امکان بارگذاری تاریخچه بازنگری وجود ندارد.',
        ),
      )
      .finally(() => setIsLoading(false))
  }, [document.key])

  useEffect(() => {
    loadHistory()
  }, [loadHistory])

  async function handleView(doc: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(doc.key)
    setFileError(null)
    try {
      const opened = await viewDocumentFile(doc.key)
      // اگر مرورگر تب را بلاک کند، به‌جای پیام خطا خودِ فایل دانلود می‌شود - همان
      // نتیجه‌ای که کاربر می‌خواست، بدون اینکه مجبور شود دکمه‌ی دیگری بزند.
      if (!opened) await downloadDocumentFile(doc.key, doc.fileName ?? doc.number)
    } catch (err) {
      setFileError(err instanceof ApiError ? err.message : 'امکان باز کردن فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  async function handleDownload(doc: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(doc.key)
    setFileError(null)
    try {
      await downloadDocumentFile(doc.key, doc.fileName ?? doc.number)
    } catch (err) {
      setFileError(err instanceof ApiError ? err.message : 'امکان دانلود فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  return (
    <Modal
      title={
        <>
          تاریخچه بازنگری
          <span className="modal-title-badge mono">{toPersianDigits(document.number)}</span>
        </>
      }
      subtitle="هر بازنگری فایل خودش را نگه می‌دارد، پس همه نسخه‌ها مستقلاً قابل مشاهده‌اند."
      size="lg"
      onClose={onClose}
    >
      <div className="modal-body">
        {fileError && (
          <div className="form-error" role="alert">
            {fileError}
          </div>
        )}

        {isLoading && <LoadingState title="در حال بارگذاری تاریخچه..." />}

        {!isLoading && loadError && (
          <ErrorState
            title={loadError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={loadHistory}>
                تلاش مجدد
              </button>
            }
          />
        )}

        {!isLoading && !loadError && revisions && (
          <ol className="revision-list">
            {revisions.map((rev) => (
              <li key={rev.key} className={`revision-item ${rev.key === document.key ? 'is-current-row' : ''}`}>
                <div className="revision-item__head">
                  <span className="mono">{toPersianDigits(rev.number)}</span>
                  {rev.isSuperseded ? (
                    <span className="badge badge-muted">منسوخ</span>
                  ) : (
                    <span className={`badge ${rev.isActive ? 'badge-success' : 'badge-danger'}`}>
                      {rev.isActive ? 'نسخه جاری' : 'آرشیو شده'}
                    </span>
                  )}
                </div>

                <dl className="revision-item__meta">
                  <div>
                    <dt>بازنگری</dt>
                    <dd>{revisionLabel(rev)}</dd>
                  </div>
                  <div>
                    <dt>تاریخ بازبینی</dt>
                    {/* نمایش شمسی؛ مقدار ذخیره‌شده در دیتابیس همچنان میلادی است. */}
                    <dd>{isoToJalaliText(rev.currentReviewDate ?? '') || '—'}</dd>
                  </div>
                  <div>
                    <dt>نام</dt>
                    <dd>{rev.name}</dd>
                  </div>
                  <div>
                    <dt>فایل</dt>
                    <dd className="mono">{rev.fileName ?? '—'}</dd>
                  </div>
                </dl>

                <div className="revision-item__actions">
                  <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    onClick={() => handleView(rev)}
                    disabled={pendingFileId !== null}
                  >
                    {pendingFileId === rev.key ? 'در حال باز کردن...' : 'مشاهده'}
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    onClick={() => handleDownload(rev)}
                    disabled={pendingFileId !== null}
                  >
                    دانلود
                  </button>
                </div>
              </li>
            ))}
          </ol>
        )}
      </div>

      <div className="modal-footer">
        <button type="button" className="btn btn-secondary" onClick={onClose}>
          بستن
        </button>
      </div>
    </Modal>
  )
}
