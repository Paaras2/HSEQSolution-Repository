import { useState } from 'react'
import type { DragEvent, FormEvent } from 'react'
import { documentApi } from '../api/documentApi'
import { ApiError } from '../lib/httpClient'
import { documentVersionLabel } from '../types/api'
import type { DocumentDto } from '../types/api'
import { Modal } from './Modal'

// Creating a document is not handled here - it has its own route
// (/documents/new), because it also collects the master-data classification that
// permanently determines the Document Number.
export type DocumentFormMode = 'edit' | 'revise'

interface DocumentFormDrawerProps {
  mode: DocumentFormMode
  document: DocumentDto
  onClose: () => void
  onSaved: () => void
}

const MODE_COPY: Record<DocumentFormMode, { title: string; subtitle: string; submit: string; busy: string }> = {
  edit: {
    title: 'ویرایش سند',
    subtitle: 'شماره، پروژه و طبقه‌بندی سازمانی پس از ایجاد سند قابل تغییر نیستند.',
    submit: 'ذخیره تغییرات',
    busy: 'در حال ذخیره…',
  },
  revise: {
    title: 'ثبت بازنگری',
    subtitle: 'یک سند جدید با شماره بازنگری بعدی ساخته می‌شود؛ نسخه فعلی در تاریخچه می‌ماند.',
    submit: 'ثبت بازنگری',
    busy: 'در حال ثبت بازنگری…',
  },
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} بایت`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} کیلوبایت`
  return `${(bytes / (1024 * 1024)).toFixed(1)} مگابایت`
}

// Edit only exposes what UpdateDocumentRequestModel actually accepts:
// Number/Project/organizational classification are immutable after creation, so
// those fields simply don't appear. Revise is different again: it issues the
// NEXT revision as a new document, so it always requires a file and never
// touches the document being revised.
export function DocumentFormDrawer({ mode, document, onClose, onSaved }: DocumentFormDrawerProps) {
  const [name, setName] = useState(document.name ?? '')
  // A revision revises *from* the previous revision's review date, so that date
  // becomes the new FormerReviewDate and the new review date starts empty. The
  // server applies the same default when the field is left blank.
  const [formerReviewDate, setFormerReviewDate] = useState(
    mode === 'revise'
      ? (document.currentReviewDate?.slice(0, 10) ?? '')
      : (document.formerReviewDate?.slice(0, 10) ?? ''),
  )
  const [currentReviewDate, setCurrentReviewDate] = useState(
    mode === 'revise' ? '' : (document.currentReviewDate?.slice(0, 10) ?? ''),
  )
  const [isActive, setIsActive] = useState(document.isActive ?? true)
  const [file, setFile] = useState<File | null>(null)
  const [isDraggingFile, setIsDraggingFile] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const copy = MODE_COPY[mode]

  function handleFileDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault()
    setIsDraggingFile(false)
    if (isSubmitting) return
    const dropped = event.dataTransfer.files?.[0]
    if (dropped) setFile(dropped)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (isSubmitting) return

    if (!name.trim()) {
      setError('نام الزامی است.')
      return
    }

    // Mirrors RevisionFileRequiredException server-side: a revision is a new
    // content version, so it cannot reuse the previous revision's file.
    if (mode === 'revise' && !file) {
      setError('برای ثبت بازنگری، بارگذاری فایل جدید الزامی است.')
      return
    }

    setIsSubmitting(true)
    setError(null)
    try {
      if (mode === 'revise') {
        await documentApi.revise({
          key: document.key,
          name: name.trim(),
          formerReviewDate: formerReviewDate || null,
          currentReviewDate: currentReviewDate || null,
          file: file as File,
        })
      } else {
        await documentApi.update({
          key: document.key,
          isActive,
          name: name.trim(),
          formerReviewDate: formerReviewDate || null,
          currentReviewDate: currentReviewDate || null,
        })
      }
      onSaved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'امکان ذخیره سند وجود ندارد. لطفاً دوباره تلاش کنید.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal
      title={
        <>
          {copy.title}
          <span className="modal-title-badge mono">{document.number}</span>
        </>
      }
      subtitle={copy.subtitle}
      onClose={onClose}
    >
      <form className="modal-form" onSubmit={handleSubmit}>
        <div className="modal-body">
          {error && (
            <div className="form-error" role="alert">
              {error}
            </div>
          )}

          {mode === 'revise' && (
            <div className="form-note">
              بازنگری فعلی <strong>
                {documentVersionLabel(document.lastVersion)}
                {document.contentRevision ? String(document.contentRevision).padStart(2, '0') : ''}
              </strong> است. با ثبت این فرم یک سند جدید با شماره بازنگری بعدی ساخته می‌شود و
              سند <strong>{document.number}</strong> به‌عنوان نسخه‌ی منسوخ در تاریخچه باقی می‌ماند.
              شماره‌ی جدید سمت سرور تولید می‌شود.
            </div>
          )}

          <section className="form-section">
            <h3 className="form-section__title">اطلاعات سند</h3>
            <div className="form-grid">
              <div className="field span-2">
                <label htmlFor="doc-name">
                  نام <span className="required-mark" aria-hidden="true">*</span>
                </label>
                <input
                  id="doc-name"
                  className="text-input"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="مثلاً: دستورالعمل کنترل مدارک"
                />
              </div>
            </div>
          </section>

          <section className="form-section">
            <h3 className="form-section__title">تاریخ‌های بازبینی</h3>
            <div className="form-grid">
              <div className="field">
                <label htmlFor="doc-former-date">تاریخ بازبینی قبلی</label>
                <input
                  id="doc-former-date"
                  type="date"
                  className="text-input"
                  value={formerReviewDate}
                  onChange={(e) => setFormerReviewDate(e.target.value)}
                  disabled={isSubmitting}
                />
              </div>

              <div className="field">
                <label htmlFor="doc-current-date">تاریخ بازبینی فعلی</label>
                <input
                  id="doc-current-date"
                  type="date"
                  className="text-input"
                  value={currentReviewDate}
                  onChange={(e) => setCurrentReviewDate(e.target.value)}
                  disabled={isSubmitting}
                />
              </div>
            </div>
          </section>

          {/* Edit deliberately has no file field. The revision suffix of a Document
              Number exists to control content changes, so swapping the file without
              advancing it would defeat the whole scheme - and since files are named
              after the Number, it also overwrote the old content. */}
          {mode === 'edit' && (
            <section className="form-section">
              <h3 className="form-section__title">فایل</h3>
              <p className="field-hint">
                فایل فعلی <span className="mono">{document.fileName ?? 'ندارد'}</span> است و از این فرم
                قابل تغییر نیست. برای جایگزینی محتوای مدرک، آن را <strong>بازنگری</strong> کنید تا نسخه‌ی
                جدید با شماره بازنگری بعدی ثبت شود و نسخه‌ی فعلی در تاریخچه بماند.
              </p>
            </section>
          )}

          {mode === 'revise' && (
            <section className="form-section">
              <h3 className="form-section__title">
                فایل بازنگری جدید
                <span className="required-mark" aria-hidden="true">*</span>
              </h3>

              <label
                htmlFor="doc-file"
                className={`file-drop ${isDraggingFile ? 'is-dragging' : ''} ${file ? 'has-file' : ''}`}
                onDragOver={(e) => {
                  e.preventDefault()
                  if (!isSubmitting) setIsDraggingFile(true)
                }}
                onDragLeave={() => setIsDraggingFile(false)}
                onDrop={handleFileDrop}
              >
                <input
                  id="doc-file"
                  type="file"
                  className="file-drop__input"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  disabled={isSubmitting}
                />

                {file ? (
                  <div className="file-drop__selected">
                    <span className="file-drop__name mono">{file.name}</span>
                    <span className="file-drop__size">{formatFileSize(file.size)}</span>
                  </div>
                ) : (
                  <div className="file-drop__prompt">
                    <strong>فایل را اینجا رها کنید</strong>
                    <span>یا برای انتخاب کلیک کنید</span>
                  </div>
                )}
              </label>

              {file && (
                <button
                  type="button"
                  className="btn btn-ghost btn-sm file-drop__clear"
                  onClick={() => setFile(null)}
                  disabled={isSubmitting}
                >
                  حذف فایل انتخاب‌شده
                </button>
              )}

              <p className="field-hint">
                فایل نسخه‌ی قبلی (<span className="mono">{document.fileName ?? 'ندارد'}</span>) دست‌نخورده باقی می‌ماند.
              </p>
            </section>
          )}

          {mode === 'edit' && (
            <section className="form-section">
              <h3 className="form-section__title">وضعیت</h3>
              <label className={`check-row ${document.isSuperseded ? 'is-disabled' : ''}`}>
                <input
                  type="checkbox"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  // A superseded revision cannot be re-activated - the server
                  // rejects it, so don't present it as an available choice.
                  disabled={isSubmitting || document.isSuperseded}
                />
                <span className="check-row__text">
                  <strong>سند فعال است</strong>
                  <span>
                    {document.isSuperseded
                      ? 'این نسخه بازنگری شده است و وضعیت آن قابل تغییر نیست. برای نسخه‌ی جدید، آخرین بازنگری را بازنگری کنید.'
                      : 'اسناد غیرفعال از فهرست پیش‌فرض حذف می‌شوند اما حذف نمی‌شوند.'}
                  </span>
                </span>
              </label>
            </section>
          )}
        </div>

        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose} disabled={isSubmitting}>
            انصراف
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting ? copy.busy : copy.submit}
          </button>
        </div>
      </form>
    </Modal>
  )
}
