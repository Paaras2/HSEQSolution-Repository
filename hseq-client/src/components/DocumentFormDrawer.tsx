import { useCallback, useEffect, useRef, useState } from 'react'
import type { DragEvent, FormEvent } from 'react'
import { documentApi } from '../api/documentApi'
import { documentRelationApi } from '../api/documentRelationApi'
import { ApiError } from '../lib/httpClient'
import { documentRelationTypeLabel, documentVersionLabel } from '../types/api'
import type { DocumentDto, DocumentRelation } from '../types/api'
import { DocumentRelationPicker } from './DocumentRelationPicker'
import { PersianDatePicker } from './PersianDatePicker'
import type { PickedRelation } from './DocumentRelationPicker'
import { Modal } from './Modal'
import { toPersianDigits } from '../lib/digits'
import { reviewDateOrderError } from '../lib/reviewDates'

// Creating a document is not handled here - it has its own route
// (/documents/new), because it also collects the master-data classification that
// permanently determines the Document Number.
//
// یک فرم، یک عملیات: پیش‌تر «ویرایش» و «بازنگری» دو دکمه و دو دیالوگ جدا بودند، در
// حالی که از دید کاربر هر دو یک کارند - اصلاح یک سند. حالا هر دو از همین دیالوگ
// انجام می‌شوند: اگر فایل تازه بارگذاری شود، بازنگری واقعی رخ می‌دهد (سند جدید با
// شماره‌ی بعدی)، وگرنه فقط اطلاعات همین سند به‌روزرسانی می‌شود. تصمیم از خودِ فرم
// می‌آید (وجود فایل)، نه از یک پرچمِ بیرونی - چون همان چیزی است که واقعاً فرق را
// می‌سازد: شماره‌ی نسخه فقط باید وقتی جلو برود که محتوا واقعاً عوض شده.
//
// شماره‌ی بازنگریِ سند دقیقاً همین را کنترل می‌کند، پس اجازه‌ی جایگزینیِ فایل بدون
// جلو رفتنِ آن شماره کلِ این سازوکار را بی‌اثر می‌کرد - برای همین فایل هنوز هم مسیر
// جداگانه‌ی خودش (ReviseAsync) را دارد، فقط دیگر پشتِ دکمه‌ی جدا نیست.
interface DocumentFormDrawerProps {
  document: DocumentDto
  onClose: () => void
  onSaved: () => void
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${toPersianDigits(bytes)} بایت`
  if (bytes < 1024 * 1024) return `${toPersianDigits((bytes / 1024).toFixed(1))} کیلوبایت`
  return `${toPersianDigits((bytes / (1024 * 1024)).toFixed(1))} مگابایت`
}

export function DocumentFormDrawer({ document, onClose, onSaved }: DocumentFormDrawerProps) {
  // بازنگریِ دوباره‌ی یک نسخه‌ی منسوخ زنجیره را شاخه‌شاخه می‌کند و سرور آن را رد
  // می‌کند؛ آرشیوشده هم تا فعال نشود بازنگری نمی‌شود (تصمیم UI، نه محدودیت سرور،
  // اما همان قاعده‌ای که پیش از یکی‌شدنِ این فرم برقرار بود). در هر دو حالت بخشِ
  // فایل غیرفعال می‌شود؛ بقیه‌ی فرم (اطلاعات، تاریخ‌ها، وضعیت، ارتباط‌ها) همچنان کار می‌کند.
  const canRevise = document.isActive && !document.isSuperseded

  const initialFormerReviewDate = document.formerReviewDate?.slice(0, 10) ?? ''
  const initialCurrentReviewDate = document.currentReviewDate?.slice(0, 10) ?? ''

  const [name, setName] = useState(document.name ?? '')
  const [formerReviewDate, setFormerReviewDate] = useState(initialFormerReviewDate)
  const [currentReviewDate, setCurrentReviewDate] = useState(initialCurrentReviewDate)
  const [isActive, setIsActive] = useState(document.isActive ?? true)
  const [file, setFile] = useState<File | null>(null)
  const [isDraggingFile, setIsDraggingFile] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // فایل یعنی این ثبت واقعاً یک بازنگری است، نه فقط اصلاح اطلاعات. کپی، بنر و دکمه‌ی
  // ثبت همه از همین یک مقدار مشتق می‌شوند تا حرفشان با هم یکی بماند.
  const willRevise = canRevise && file !== null

  // لحظه‌ای که کاربر برای اولین‌بار فایل پیوست می‌کند، تاریخ‌ها به قاعده‌ی بازنگری
  // می‌روند: «بازبینی جاری» قبلی می‌شود «بازبینی قبلی»ِ نسخه‌ی تازه، و «جاری» خالی
  // می‌ماند تا کاربر تاریخ تازه انتخاب کند - اما فقط اگر کاربر خودش قبلاً دستی چیزی
  // در این دو فیلد تغییر نداده باشد؛ وگرنه روی انتخاب او می‌نشست.
  const dateDefaultsShiftedRef = useRef(false)
  useEffect(() => {
    if (!file || dateDefaultsShiftedRef.current) return
    dateDefaultsShiftedRef.current = true
    setFormerReviewDate((prev) => (prev === initialFormerReviewDate ? initialCurrentReviewDate || prev : prev))
    setCurrentReviewDate((prev) => (prev === initialCurrentReviewDate ? '' : prev))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [file])

  // مدارک مرتبط - همیشه بارگذاری می‌شوند مگر روی نسخه‌ی منسوخ، چون بازنگریِ بعدی
  // ارتباط‌ها را خودش با خودش جلو می‌برد و ردیف تازه همان‌جا جا می‌ماند.
  const [relations, setRelations] = useState<DocumentRelation[] | null>(null)
  const [isRelationBusy, setIsRelationBusy] = useState(false)
  const [relationError, setRelationError] = useState<string | null>(null)
  const [pendingRemoveId, setPendingRemoveId] = useState<string | null>(null)

  const loadRelations = useCallback(() => {
    if (document.isSuperseded) return
    setRelationError(null)
    documentRelationApi
      .getForDocument(document.key)
      .then(setRelations)
      .catch((err) =>
        setRelationError(err instanceof ApiError ? err.message : 'امکان بارگذاری مدارک مرتبط وجود ندارد.'),
      )
  }, [document.key, document.isSuperseded])

  useEffect(() => {
    loadRelations()
  }, [loadRelations])

  // ثبت فوری - مقدار بازگشتی true یعنی انتخابگر ورودی‌هایش را پاک کند.
  async function handleAddRelation(picked: PickedRelation): Promise<boolean> {
    if (isRelationBusy) return false

    setIsRelationBusy(true)
    setRelationError(null)
    try {
      await documentRelationApi.add({
        sourceDocumentId: document.key,
        targetDocumentId: picked.target.key,
        relationType: picked.relationType,
        note: picked.note,
      })
      loadRelations()
      return true
    } catch (err) {
      setRelationError(err instanceof ApiError ? err.message : 'امکان ثبت ارتباط وجود ندارد.')
      return false
    } finally {
      setIsRelationBusy(false)
    }
  }

  async function handleRemoveRelation(relation: DocumentRelation) {
    setPendingRemoveId(relation.key)
    setRelationError(null)
    try {
      await documentRelationApi.remove(relation.key)
      loadRelations()
    } catch (err) {
      setRelationError(err instanceof ApiError ? err.message : 'امکان حذف ارتباط وجود ندارد.')
    } finally {
      setPendingRemoveId(null)
    }
  }

  function handleFileDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault()
    setIsDraggingFile(false)
    if (isSubmitting || !canRevise) return
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

    const reviewDateError = reviewDateOrderError(formerReviewDate, currentReviewDate)
    if (reviewDateError) {
      setError(reviewDateError)
      return
    }

    setIsSubmitting(true)
    setError(null)
    try {
      if (willRevise) {
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

  const subtitle = willRevise
    ? 'یک سند جدید با شماره بازنگری بعدی ساخته می‌شود؛ نسخه فعلی در تاریخچه می‌ماند.'
    : canRevise
      ? 'شماره، پروژه و طبقه‌بندی سازمانی پس از ایجاد سند قابل تغییر نیستند. برای ثبت محتوای تازه، در بخش «فایل» پایین یک فایل بارگذاری کنید.'
      : document.isSuperseded
        ? 'این نسخه پیش‌تر بازنگری شده و محتوایش دیگر قابل تغییر نیست؛ فقط اطلاعاتش را می‌توانید اصلاح کنید.'
        : 'این سند آرشیو شده است. محتوایش تا فعال‌سازی دوباره (پایین فرم) قابل بازنگری نیست.'

  return (
    <Modal
      title={
        <>
          بازنگری سند
          <span className="modal-title-badge mono">{toPersianDigits(document.number)}</span>
        </>
      }
      subtitle={subtitle}
      onClose={onClose}
    >
      <form className="modal-form" onSubmit={handleSubmit}>
        <div className="modal-body">
          {error && (
            <div className="form-error" role="alert">
              {error}
            </div>
          )}

          {willRevise && (
            <div className="form-note">
              بازنگری فعلی <strong>
                {documentVersionLabel(document.lastVersion)}
                {document.contentRevision ? toPersianDigits(String(document.contentRevision).padStart(2, '0')) : ''}
              </strong> است. با ثبت این فرم یک سند جدید با شماره بازنگری بعدی ساخته می‌شود و
              سند <strong>{toPersianDigits(document.number)}</strong> به‌عنوان نسخه‌ی منسوخ در تاریخچه باقی می‌ماند.
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
                {/* نمایش شمسی، مقدار میلادی - همان «yyyy-MM-dd» که به سرور می‌رود. */}
                <PersianDatePicker
                  id="doc-former-date"
                  value={formerReviewDate}
                  onChange={setFormerReviewDate}
                  disabled={isSubmitting}
                />
              </div>

              <div className="field">
                <label htmlFor="doc-current-date">تاریخ بازبینی فعلی</label>
                <PersianDatePicker
                  id="doc-current-date"
                  value={currentReviewDate}
                  onChange={setCurrentReviewDate}
                  disabled={isSubmitting}
                />
              </div>
            </div>
          </section>

          <section className="form-section">
            <h3 className="form-section__title">
              فایل
              {canRevise && <span className="field-hint field-hint--inline">(اختیاری - برای ثبت بازنگری)</span>}
            </h3>

            {!canRevise && (
              <p className="field-hint">
                فایل فعلی <span className="mono">{document.fileName ?? 'ندارد'}</span> است و از این فرم قابل تغییر
                نیست.{' '}
                {document.isSuperseded
                  ? 'این نسخه بازنگری شده؛ برای ثبت محتوای تازه، آخرین بازنگریِ زنجیره را باز کنید.'
                  : 'برای بازنگری، ابتدا سند را از بخش «وضعیت» پایین‌تر فعال کنید.'}
              </p>
            )}

            {canRevise && (
              <>
                <p className="field-hint">
                  این بخش را خالی بگذارید تا فقط اطلاعات بالا ذخیره شود، بدون ساختن نسخه‌ی تازه. برای ثبت بازنگری، فایل
                  جدید محتوا را همین‌جا بارگذاری کنید.
                </p>

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
                  <>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm file-drop__clear"
                      onClick={() => setFile(null)}
                      disabled={isSubmitting}
                    >
                      حذف فایل انتخاب‌شده
                    </button>
                    <p className="field-hint">
                      فایل نسخه‌ی قبلی (<span className="mono">{document.fileName ?? 'ندارد'}</span>) دست‌نخورده باقی
                      می‌ماند.
                    </p>
                  </>
                )}
              </>
            )}
          </section>

          {/* مدارک مرتبط، مگر روی نسخه‌ی منسوخ: بازنگری بعدی ارتباط‌ها را با خودش
              جلو می‌برد، پس ردیف تازه همان‌جا جا می‌ماند. */}
          {!document.isSuperseded && (
            <section className="form-section">
              <h3 className="form-section__title">مدارک مرتبط</h3>
              <p className="field-hint">
                ارتباط‌ها بلافاصله ثبت می‌شوند و به دکمه‌ی ذخیره‌ی پایین فرم وابسته نیستند.
              </p>

              {relationError && (
                <div className="form-error" role="alert">
                  {relationError}
                </div>
              )}

              {relations && relations.length > 0 && (
                <ul className="relation-list">
                  {relations.map((relation) => (
                    <li key={relation.key} className="relation-item">
                      <div className="relation-item__head">
                        <span className="mono">{toPersianDigits(relation.number)}</span>
                        <span className="badge badge-muted">
                          {documentRelationTypeLabel(relation.relationType, relation.isOutgoing)}
                        </span>
                      </div>

                      <div className="relation-item__title">{relation.name}</div>

                      {relation.note && <p className="relation-item__note">{relation.note}</p>}

                      <div className="relation-item__actions">
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleRemoveRelation(relation)}
                          disabled={pendingRemoveId === relation.key || isSubmitting}
                        >
                          {pendingRemoveId === relation.key ? 'در حال حذف...' : 'حذف ارتباط'}
                        </button>
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              <DocumentRelationPicker
                excludedDocumentIds={[document.key, ...(relations ?? []).map((r) => r.documentId)]}
                disabled={isSubmitting}
                isSubmitting={isRelationBusy}
                error={null}
                submitLabel="افزودن ارتباط"
                submittingLabel="در حال ثبت..."
                onPick={handleAddRelation}
              />
            </section>
          )}

          {/* وقتی فایل پیوست شده، این سند بعد از ثبت خودکار منسوخ می‌شود - تغییر
              دستیِ وضعیتش همان لحظه دیگر معنا ندارد. */}
          {!willRevise && (
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
                      : 'اسناد آرشیوشده از فهرست پیش‌فرض کنار می‌روند اما پاک نمی‌شوند.'}
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
            {isSubmitting ? (willRevise ? 'در حال ثبت بازنگری…' : 'در حال ذخیره…') : willRevise ? 'ثبت بازنگری' : 'ذخیره تغییرات'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
