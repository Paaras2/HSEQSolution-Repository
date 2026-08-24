import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { documentRelationApi } from '../api/documentRelationApi'
import { downloadDocumentFile, viewDocumentFile } from '../lib/documentFile'
import { ApiError } from '../lib/httpClient'
import { documentRelationTypeDescription, documentRelationTypeLabel } from '../types/api'
import type { DocumentDto, DocumentRelation } from '../types/api'
import { DocumentRelationPicker } from './DocumentRelationPicker'
import type { PickedRelation } from './DocumentRelationPicker'
import { Modal } from './Modal'
import { LoadingState, EmptyState, ErrorState } from './StateViews'
import { toPersianDigits } from '../lib/digits'

interface RelatedDocumentsDrawerProps {
  document: DocumentDto
  onClose: () => void
}

// مدارک مرتبطِ یک مدرک: شبکه‌ی ارجاع میان مدارک مستقل - نه نسخه‌های خودِ همین مدرک،
// که کار RevisionHistoryDrawer است.
//
// فهرست، ارتباط‌های هر دو سمت را نشان می‌دهد و همیشه مشخصات مدرکِ مقابل را می‌آورد. برای
// نوع‌های جهت‌دار، برچسب از سمت مقصد معکوس خوانده می‌شود (documentRelationTypeLabel) تا
// کاربر مجبور نباشد بداند ردیف در کدام جهت ذخیره شده است.
//
// نام پارامتر به currentDocument تغییر داده شده تا با document سراسری مرورگر اشتباه نشود.
export function RelatedDocumentsDrawer({ document: currentDocument, onClose }: RelatedDocumentsDrawerProps) {
  const { hasCapability } = useAuth()
  const canManage = hasCapability('documents:manage')

  const [relations, setRelations] = useState<DocumentRelation[] | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [isSubmitting, setIsSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const [pendingFileId, setPendingFileId] = useState<string | null>(null)
  const [pendingRemoveId, setPendingRemoveId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)


  const loadRelations = useCallback(() => {
    setIsLoading(true)
    setLoadError(null)
    documentRelationApi
      .getForDocument(currentDocument.key)
      .then(setRelations)
      .catch((err) =>
        setLoadError(err instanceof ApiError ? err.message : 'امکان بارگذاری مدارک مرتبط وجود ندارد.'),
      )
      .finally(() => setIsLoading(false))
  }, [currentDocument.key])

  useEffect(() => {
    loadRelations()
  }, [loadRelations])

  // ثبت فوری روی سرور - برخلاف فرم افزودن سند، اینجا مدرک از قبل کلید دارد.
  async function handleAdd(picked: PickedRelation): Promise<boolean> {
    if (isSubmitting) return false

    setIsSubmitting(true)
    setFormError(null)
    try {
      await documentRelationApi.add({
        sourceDocumentId: currentDocument.key,
        targetDocumentId: picked.target.key,
        relationType: picked.relationType,
        note: picked.note,
      })
      loadRelations()
      return true
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : 'امکان ثبت ارتباط وجود ندارد.')
      return false
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleRemove(relation: DocumentRelation) {
    const confirmed = window.confirm(
      'ارتباط با مدرک ' + relation.number + ' حذف شود؟ خودِ مدرک حذف نمی‌شود، فقط این ارتباط برداشته می‌شود.',
    )
    if (!confirmed) return

    setPendingRemoveId(relation.key)
    setActionError(null)
    try {
      await documentRelationApi.remove(relation.key)
      loadRelations()
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'امکان حذف ارتباط وجود ندارد.')
    } finally {
      setPendingRemoveId(null)
    }
  }

  async function handleView(relation: DocumentRelation) {
    if (pendingFileId) return
    setPendingFileId(relation.documentId)
    setActionError(null)
    try {
      const opened = await viewDocumentFile(relation.documentId)
      // اگر مرورگر تب را بلاک کند، به‌جای پیام خطا خودِ فایل دانلود می‌شود - همان
      // نتیجه‌ای که کاربر می‌خواست، بدون اینکه مجبور شود دکمه‌ی دیگری بزند.
      if (!opened) await downloadDocumentFile(relation.documentId, relation.number)
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'امکان باز کردن فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  async function handleDownload(relation: DocumentRelation) {
    if (pendingFileId) return
    setPendingFileId(relation.documentId)
    setActionError(null)
    try {
      await downloadDocumentFile(relation.documentId, relation.number)
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'امکان دانلود فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  return (
    <Modal
      title={
        <>
          مدارک مرتبط
          <span className="modal-title-badge mono">{toPersianDigits(currentDocument.number)}</span>
        </>
      }
      subtitle="ارجاع میان مدارک مستقل - مثلاً یک دستورالعمل و فرمِ آن. برای نسخه‌های همین مدرک، «تاریخچه» را ببینید."
      size="lg"
      onClose={onClose}
    >
      <div className="modal-body">
        {/* روی یک نسخه‌ی منسوخ ارتباط تازه ثبت نمی‌شود: بازنگری بعدی ارتباط‌ها را با خودش
            جلو می‌برد، پس چنین ردیفی همان‌جا روی نسخه‌ی از رده خارج جا می‌ماند. همان منطقِ
            پنهان‌کردن دکمه‌ی «بازنگری» برای نسخه‌های منسوخ در فهرست اسناد. */}
        {currentDocument.isSuperseded && (
          <div className="form-note">
            این یک نسخه‌ی منسوخ است. ارتباط‌های آن هنگام بازنگری به آخرین نسخه منتقل شده‌اند؛ برای
            ثبت ارتباط جدید، «مدارک مرتبط» را روی آخرین بازنگری باز کنید.
          </div>
        )}

        {canManage && !currentDocument.isSuperseded && (
          <section className="form-section">
            <h3 className="form-section__title">افزودن ارتباط</h3>
            <DocumentRelationPicker
              // خودِ مدرک و مدارکی که از قبل مرتبط شده‌اند از پیشنهادها کنار گذاشته
              // می‌شوند تا کاربر گزینه‌ای را نبیند که سرور قطعاً ردش می‌کند.
              excludedDocumentIds={[currentDocument.key, ...(relations ?? []).map((r) => r.documentId)]}
              isSubmitting={isSubmitting}
              error={formError}
              submitLabel="افزودن ارتباط"
              submittingLabel="در حال ثبت..."
              onPick={handleAdd}
            />
          </section>
        )}

        {actionError && (
          <div className="form-error" role="alert">
            {actionError}
          </div>
        )}

        {isLoading && <LoadingState title="در حال بارگذاری مدارک مرتبط..." />}

        {!isLoading && loadError && (
          <ErrorState
            title={loadError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={loadRelations}>
                تلاش مجدد
              </button>
            }
          />
        )}

        {!isLoading && !loadError && relations && relations.length === 0 && (
          <EmptyState
            title={
              currentDocument.isSuperseded
                ? 'روی این نسخه مدرک مرتبطی نمانده است.'
                : 'هنوز مدرک مرتبطی ثبت نشده است.'
            }
            description={
              currentDocument.isSuperseded || canManage
                ? undefined
                : 'ثبت ارتباط نیازمند دسترسی مدیریت اسناد است.'
            }
          />
        )}

        {!isLoading && !loadError && relations && relations.length > 0 && (
          <ul className="relation-list">
            {relations.map((relation) => (
              <li key={relation.key} className="relation-item">
                <div className="relation-item__head">
                  <span className="mono">{toPersianDigits(relation.number)}</span>
                  <span
                    className="badge badge-muted"
                    title={documentRelationTypeDescription(relation.relationType, relation.isOutgoing)}
                  >
                    {documentRelationTypeLabel(relation.relationType, relation.isOutgoing)}
                  </span>
                  {/* یک نسخه‌ی منسوخ هم غیرفعال است، پس اول باید همان بررسی شود -
                      وگرنه «بازنگری شده» به‌اشتباه «حذف‌شده» خوانده می‌شود. */}
                  {relation.isSuperseded ? (
                    <span className="badge badge-danger">منسوخ (بازنگری شده)</span>
                  ) : (
                    !relation.isActive && <span className="badge badge-danger">غیرفعال</span>
                  )}
                </div>

                <div className="relation-item__title">{relation.name}</div>

                {relation.note && <p className="relation-item__note">{relation.note}</p>}

                <div className="relation-item__actions">
                  <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    onClick={() => handleView(relation)}
                    disabled={pendingFileId !== null}
                  >
                    {pendingFileId === relation.documentId ? 'در حال باز کردن...' : 'مشاهده'}
                  </button>
                  <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    onClick={() => handleDownload(relation)}
                    disabled={pendingFileId !== null}
                  >
                    دانلود
                  </button>
                  {canManage && (
                    <button
                      type="button"
                      className="btn btn-danger btn-sm"
                      onClick={() => handleRemove(relation)}
                      disabled={pendingRemoveId === relation.key}
                    >
                      {pendingRemoveId === relation.key ? 'در حال حذف...' : 'حذف ارتباط'}
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
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
