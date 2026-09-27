import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { documentApi } from '../api/documentApi'
import { masterDataApi } from '../api/masterDataApi'
import { ApiError } from '../lib/httpClient'
import { documentVersionLabel } from '../types/api'
import type { DocumentDto } from '../types/api'
import type { DocumentLookups } from './DocumentCreatePage'
import { LoadingState, ErrorState } from '../components/StateViews'
import { toLatinDigits, toPersianDigits } from '../lib/digits'
import { sortedByTitle } from '../lib/sorting'

// حرف‌های بازنگری A تا Z، برای گزینه‌های select - HSEQ.Common.DocumentVersion.
const MAJOR_VERSION_OPTIONS = Array.from({ length: 26 }, (_, i) => i + 1)

// ویرایشِ کدگذاریِ یک سندِ موجود - عملیاتی استثنایی که فقط مدیر اسناد/سیستم می‌بیند
// (همان دروازه‌ی admin:access که داشبورد و پنل ادمین دارند). برخلافِ همه‌ی مسیرهای
// دیگر که Number را فقط سرور و فقط طبق قاعده می‌سازد، اینجا مدیر می‌تواند طبقه‌بندی
// (مدیریت/فعالیت/نوع سند) + سریال، و/یا حرفِ بازنگری را مستقیم و به صلاحدیدِ خودش
// تغییر دهد. برای همین صفحه‌ی جداگانه است نه دیالوگ: مثل ساختنِ سند، طبقه‌بندی‌ای را
// جمع می‌کند که شکل شماره را عوض می‌کند - و پیامدش (تغییرِ خودِ شماره) باید صریح دیده شود.
export function DocumentCodingPage() {
  const { documentId } = useParams<{ documentId: string }>()
  const navigate = useNavigate()

  const [document, setDocument] = useState<DocumentDto | null>(null)
  const [lookups, setLookups] = useState<DocumentLookups | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const [managementId, setManagementId] = useState('')
  const [activityId, setActivityId] = useState('')
  const [documentTypeId, setDocumentTypeId] = useState('')
  const [serialText, setSerialText] = useState('')
  const [majorVersion, setMajorVersion] = useState(1)
  const [contentRevisionText, setContentRevisionText] = useState('')
  const [confirmed, setConfirmed] = useState(false)

  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function load() {
    if (!documentId) return
    setIsLoading(true)
    setLoadError(null)
    Promise.all([
      documentApi.getById(documentId),
      masterDataApi.getProjects(),
      masterDataApi.getManagements(),
      masterDataApi.getActivities(),
      masterDataApi.getDocumentTypes(),
    ])
      .then(([doc, projects, managements, activities, documentTypes]) => {
        setDocument(doc)
        setLookups({ projects, managements, activities, documentTypes })
        setManagementId(doc.organizationalManagementId)
        setActivityId(doc.organizationalActivityId)
        setDocumentTypeId(doc.documentTypeId)
        setSerialText(toPersianDigits(doc.serialNumber))
        setMajorVersion(doc.lastVersion)
        setContentRevisionText(doc.contentRevision ? toPersianDigits(doc.contentRevision) : '')
      })
      .catch((err) => setLoadError(err instanceof ApiError ? err.message : 'امکان بارگذاری سند وجود ندارد.'))
      .finally(() => setIsLoading(false))
  }

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(load, [documentId])

  const activitiesForManagement = sortedByTitle(
    lookups?.activities.filter((a) => a.organizationalManagementId === managementId) ?? [],
  )

  // پروژه‌ی سند (اگر «پروژه» باشد) از این فرم قابل تغییر نیست، پس isProjectRelated
  // ثابت است - فقط تعیین می‌کند که فیلدِ «شماره‌ی فرعی» اصلاً نمایش داده شود یا نه.
  // category عددِ خام است (HSEQ.Common.DocumentCategory): 1 = Project.
  const isProjectRelated =
    document?.category === 1
      ? (lookups?.projects.find((p) => p.key === document.projectId)?.isProjectRelated ?? false)
      : false

  const parsedSerial = serialText.trim() === '' ? null : Number(toLatinDigits(serialText))
  const parsedContentRevision = contentRevisionText.trim() === '' ? null : Number(toLatinDigits(contentRevisionText))

  const classificationChanged = useMemo(() => {
    if (!document) return false
    return (
      managementId !== document.organizationalManagementId ||
      activityId !== document.organizationalActivityId ||
      documentTypeId !== document.documentTypeId ||
      parsedSerial !== document.serialNumber
    )
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [document, managementId, activityId, documentTypeId, parsedSerial])

  const revisionChanged = useMemo(() => {
    if (!document) return false
    return majorVersion !== document.lastVersion || parsedContentRevision !== (document.contentRevision ?? null)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [document, majorVersion, parsedContentRevision])

  const hasAnyChange = classificationChanged || revisionChanged

  const serialInvalid = parsedSerial !== null && (parsedSerial < 1 || parsedSerial > 999 || !Number.isInteger(parsedSerial))
  const contentRevisionInvalid =
    parsedContentRevision !== null &&
    (parsedContentRevision < 1 || parsedContentRevision > 99 || !Number.isInteger(parsedContentRevision))

  const canSubmit =
    hasAnyChange &&
    confirmed &&
    !serialInvalid &&
    !contentRevisionInvalid &&
    Boolean(managementId) &&
    Boolean(activityId) &&
    Boolean(documentTypeId)

  async function handleSubmit() {
    if (!document || !canSubmit || isSubmitting) return

    setIsSubmitting(true)
    setError(null)
    try {
      await documentApi.editCoding({
        key: document.key,
        classification: classificationChanged
          ? { organizationalManagementId: managementId, organizationalActivityId: activityId, documentTypeId, serialNumber: parsedSerial }
          : null,
        revision: revisionChanged ? { lastVersion: majorVersion, contentRevision: parsedContentRevision } : null,
      })
      navigate('/documents')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'امکان ذخیره کدگذاری وجود ندارد. لطفاً دوباره تلاش کنید.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="create-page">
      <div className="page-header">
        <div>
          <Link to="/documents" className="page-back">
            <span aria-hidden="true">›</span> بازگشت به فهرست اسناد
          </Link>
          <h1>ویرایش کدگذاری سند</h1>
          <p>
            {document ? (
              <>
                سند <span className="mono">{toPersianDigits(document.number)}</span> - این عملیات شماره‌ی سند را
                تغییر می‌دهد.
              </>
            ) : (
              'عملیاتی استثنایی برای اصلاحِ یک کدگذاریِ اشتباه.'
            )}
          </p>
        </div>
      </div>

      {isLoading && (
        <div className="card">
          <LoadingState title="در حال بارگذاری..." />
        </div>
      )}

      {!isLoading && loadError && (
        <div className="card">
          <ErrorState
            title={loadError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={load}>
                تلاش مجدد
              </button>
            }
          />
        </div>
      )}

      {!isLoading && !loadError && document && lookups && (
        <div className="create-layout">
          <div className="create-main">
            <div className="form-note">
              <strong>این صفحه فقط برای اصلاحِ یک کدگذاریِ اشتباه است</strong>، نه برای ثبتِ بازنگریِ محتوایی. تغییرِ
              مدیریت/فعالیت/نوع‌سند/سریال روی <strong>کل زنجیره‌ی بازنگریِ این سند</strong> اعمال می‌شود (همه‌ی
              نسخه‌های قبلی و بعدی‌اش با هم)؛ تغییرِ حرفِ بازنگری فقط روی همینِ یک سند. دسته‌بندی (ستاد/پروژه) و
              پروژه از این فرم قابل تغییر نیستند.
            </div>

            {error && (
              <div className="form-error" role="alert">
                {error}
              </div>
            )}

            <section className="card create-card">
              <div className="create-card__head">
                <h2 className="create-card__title">طبقه‌بندی و سریال</h2>
                <span className="create-card__badge">روی کل زنجیره اعمال می‌شود</span>
              </div>

              <div className="form-grid form-grid--3">
                <div className="field">
                  <label htmlFor="coding-type">نوع سند</label>
                  <select
                    id="coding-type"
                    className="select-input"
                    value={documentTypeId}
                    onChange={(e) => setDocumentTypeId(e.target.value)}
                    disabled={isSubmitting}
                  >
                    {sortedByTitle(lookups.documentTypes).map((t) => (
                      <option key={t.key} value={t.key}>
                        {t.title} ({toPersianDigits(t.code)})
                      </option>
                    ))}
                  </select>
                </div>

                <div className="field">
                  <label htmlFor="coding-management">مدیریت سازمانی</label>
                  <select
                    id="coding-management"
                    className="select-input"
                    value={managementId}
                    onChange={(e) => {
                      setManagementId(e.target.value)
                      setActivityId('')
                    }}
                    disabled={isSubmitting}
                  >
                    {sortedByTitle(lookups.managements).map((m) => (
                      <option key={m.key} value={m.key}>
                        {m.title} ({toPersianDigits(m.code)})
                      </option>
                    ))}
                  </select>
                </div>

                <div className="field">
                  <label htmlFor="coding-activity">فعالیت سازمانی</label>
                  <select
                    id="coding-activity"
                    className="select-input"
                    value={activityId}
                    onChange={(e) => setActivityId(e.target.value)}
                    disabled={isSubmitting || !managementId}
                  >
                    {activityId === '' && <option value="">یک فعالیت انتخاب کنید…</option>}
                    {activitiesForManagement.map((a) => (
                      <option key={a.key} value={a.key}>
                        {a.title} ({toPersianDigits(a.code)})
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="form-grid">
                <div className="field">
                  <label htmlFor="coding-serial">
                    سریال (SSS) <span className="field-hint">- خالی یعنی سریالِ بعدیِ کدِ تازه خودکار تخصیص یابد</span>
                  </label>
                  <input
                    id="coding-serial"
                    className="text-input mono"
                    value={serialText}
                    onChange={(e) => setSerialText(toPersianDigits(toLatinDigits(e.target.value).replace(/\D/g, '')))}
                    disabled={isSubmitting}
                    placeholder="مثلاً ۰۰۸"
                  />
                  {serialInvalid && <p className="field-hint field-hint--error">سریال باید بین ۱ تا ۹۹۹ باشد.</p>}
                </div>
              </div>
            </section>

            <section className="card create-card">
              <div className="create-card__head">
                <h2 className="create-card__title">بازنگری</h2>
                <span className="create-card__badge">فقط همین سند</span>
              </div>

              <div className="form-grid form-grid--3">
                <div className="field">
                  <label htmlFor="coding-major">حرفِ بازنگری</label>
                  <select
                    id="coding-major"
                    className="select-input"
                    value={majorVersion}
                    onChange={(e) => setMajorVersion(Number(e.target.value))}
                    disabled={isSubmitting}
                  >
                    {MAJOR_VERSION_OPTIONS.map((v) => (
                      <option key={v} value={v}>
                        {documentVersionLabel(v)}
                      </option>
                    ))}
                  </select>
                </div>

                {isProjectRelated && (
                  <div className="field">
                    <label htmlFor="coding-content-revision">
                      شماره‌ی فرعی <span className="field-hint">(اختیاری، ۰۱ تا ۹۹)</span>
                    </label>
                    <input
                      id="coding-content-revision"
                      className="text-input mono"
                      value={contentRevisionText}
                      onChange={(e) => setContentRevisionText(toPersianDigits(toLatinDigits(e.target.value).replace(/\D/g, '')))}
                      disabled={isSubmitting}
                      placeholder="مثلاً ۰۳"
                    />
                    {contentRevisionInvalid && (
                      <p className="field-hint field-hint--error">شماره‌ی فرعی باید بین ۱ تا ۹۹ باشد.</p>
                    )}
                  </div>
                )}
              </div>
            </section>

            <section className="card create-card">
              <label className="check-row">
                <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} disabled={isSubmitting} />
                <span className="check-row__text">
                  <strong>متوجه‌ام که این کار شماره‌ی سند را تغییر می‌دهد</strong>
                  <span>شماره‌ی قبلی از این پس در جایی استفاده نمی‌شود؛ اگر جایی (مثلاً روی کاغذ) ثبت شده، باید به‌روزرسانی شود.</span>
                </span>
              </label>
            </section>
          </div>

          <aside className="create-aside">
            <div className="card create-card create-summary">
              <div className="create-card__head">
                <h2 className="create-card__title">شماره‌ی فعلی</h2>
              </div>
              <p className="code-strip mono" dir="ltr">
                {toPersianDigits(document.number)}
              </p>
              <p className="create-progress__text">
                {hasAnyChange
                  ? 'با ثبت این فرم، شماره‌ی تازه سمت سرور ساخته و اعتبارسنجی می‌شود.'
                  : 'هنوز چیزی تغییر نکرده است.'}
              </p>
            </div>
          </aside>

          <div className="create-actions">
            <p className="create-actions__hint">
              {!hasAnyChange
                ? 'چیزی برای ذخیره وجود ندارد.'
                : !confirmed
                  ? 'برای ثبت، تیک تأیید بالا را بزنید.'
                  : 'آماده‌ی ذخیره است.'}
            </p>
            <button type="button" className="btn btn-secondary" onClick={() => navigate('/documents')} disabled={isSubmitting}>
              انصراف
            </button>
            <button type="button" className="btn btn-primary" onClick={handleSubmit} disabled={!canSubmit || isSubmitting}>
              {isSubmitting ? 'در حال ذخیره…' : 'ذخیره کدگذاری'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
