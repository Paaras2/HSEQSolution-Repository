import { useEffect, useReducer, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { documentApi } from '../api/documentApi'
import { masterDataApi } from '../api/masterDataApi'
import { ApiError } from '../lib/httpClient'
import { isoToJalaliText } from '../lib/jalali'
import { sortedByTitle } from '../lib/sorting'
import { toPersianDigits } from '../lib/digits'
import { LoadingState, ErrorState } from '../components/StateViews'
import { UploadIcon } from '../components/Icons'
import {
  initialReservationState,
  reservationReducer,
  reservationFlags,
  type ReservationMetadata,
} from '../lib/documentReservation'
import type { DocumentLookups } from './DocumentCreatePage'

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${toPersianDigits(bytes)} بایت`
  if (bytes < 1024 * 1024) return `${toPersianDigits((bytes / 1024).toFixed(1))} کیلوبایت`
  return `${toPersianDigits((bytes / (1024 * 1024)).toFixed(1))} مگابایت`
}

// جریانِ «رزرو کد»: اول متادیتا و «رزرو کد» (بدون فایل) → کد نمایش داده می‌شود و فرم به
// «در انتظار فایل» می‌رود → بارگذاری فایل، مدرک را نهایی می‌کند. کلِ گذارها با یک reducer
// (lib/documentReservation) مدیریت می‌شود؛ این کامپوننت فقط رندر و ارکستریشنِ API است.
export function DocumentReservePage() {
  const navigate = useNavigate()
  const [state, dispatch] = useReducer(reservationReducer, initialReservationState)
  const flags = reservationFlags(state)

  const [lookups, setLookups] = useState<DocumentLookups | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [isLoadingLookups, setIsLoadingLookups] = useState(true)

  function loadLookups() {
    setIsLoadingLookups(true)
    setLookupError(null)
    Promise.all([
      masterDataApi.getProjects(),
      masterDataApi.getManagements(),
      masterDataApi.getActivities(),
      masterDataApi.getDocumentTypes(),
    ])
      .then(([projects, managements, activities, documentTypes]) =>
        setLookups({ projects, managements, activities, documentTypes }),
      )
      .catch((err) =>
        setLookupError(err instanceof ApiError ? err.message : 'امکان بارگذاری داده‌های پایه وجود ندارد.'),
      )
      .finally(() => setIsLoadingLookups(false))
  }

  useEffect(loadLookups, [])

  const m = state.metadata
  const setField = (field: keyof ReservationMetadata, value: string | boolean) =>
    dispatch({ type: 'FIELD_CHANGED', field, value })

  const activitiesForManagement = sortedByTitle(
    lookups?.activities.filter((a) => a.organizationalManagementId === m.managementId) ?? [],
  )

  async function reserve() {
    dispatch({ type: 'RESERVE_STARTED' })
    try {
      const res = await documentApi.reserve({
        name: m.name.trim(),
        category: m.category,
        isEnglishVersion: m.isEnglishVersion,
        projectId: m.category === 'Project' ? m.projectId : null,
        organizationalManagementId: m.managementId,
        organizationalActivityId: m.activityId,
        documentTypeId: m.documentTypeId,
      })
      dispatch({ type: 'RESERVE_SUCCEEDED', key: res.key, code: res.number, expiresAt: res.reservationExpiresAtUtc })
    } catch (err) {
      dispatch({
        type: 'RESERVE_FAILED',
        message: err instanceof ApiError ? err.message : 'رزرو کد انجام نشد. اتصال را بررسی کنید و دوباره تلاش کنید.',
      })
    }
  }

  async function finalize() {
    if (!state.reservedKey || !state.file) return
    dispatch({ type: 'UPLOAD_STARTED' })
    try {
      await documentApi.attachFile(state.reservedKey, state.file)
      dispatch({ type: 'UPLOAD_SUCCEEDED' })
    } catch (err) {
      dispatch({
        type: 'UPLOAD_FAILED',
        message:
          err instanceof ApiError ? err.message : 'بارگذاری فایل انجام نشد. کدِ رزرو محفوظ است؛ دوباره تلاش کنید.',
      })
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (flags.isBusy) return
    if (state.status === 'draft' && flags.canReserve) void reserve()
    else if (state.status === 'reserved' && flags.canFinalize) void finalize()
  }

  return (
    <div className="create-page">
      <div className="page-header">
        <div>
          <Link to="/documents" className="page-back">
            <span aria-hidden="true">›</span> بازگشت به فهرست اسناد
          </Link>
          <h1>رزرو کد مدرک</h1>
          <p>
            اول کد را رزرو کنید؛ فایل را همین حالا یا بعداً بارگذاری کنید.{' '}
            <Link to="/documents/new">ثبت سندِ کامل با فایل ←</Link>
          </p>
        </div>
      </div>

      {isLoadingLookups && (
        <div className="card">
          <LoadingState title="در حال بارگذاری داده‌های پایه…" />
        </div>
      )}

      {!isLoadingLookups && lookupError && (
        <div className="card">
          <ErrorState
            title={lookupError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={loadLookups}>
                تلاش مجدد
              </button>
            }
          />
        </div>
      )}

      {!isLoadingLookups && !lookupError && lookups && state.status !== 'done' && (
        <form className="create-panel" onSubmit={handleSubmit}>
          {state.reservedCode && (
            <div className="reserve-banner" role="status">
              <span className="reserve-banner__label">کد رزرو شد</span>
              <span className="reserve-banner__code mono">{state.reservedCode}</span>
              {state.reservationExpiresAt && (
                <span className="reserve-banner__hint">
                  تا {isoToJalaliText(state.reservationExpiresAt)} فایل را بارگذاری کنید، وگرنه رزرو منقضی می‌شود.
                </span>
              )}
            </div>
          )}

          <div className="form-grid">
            <div className="field">
              <label htmlFor="rsv-name">
                نام مدرک <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <input
                id="rsv-name"
                className="text-input"
                value={m.name}
                onChange={(e) => setField('name', e.target.value)}
                disabled={flags.metadataLocked}
                autoComplete="off"
              />
            </div>

            <div className="field">
              <label htmlFor="rsv-category">دسته‌بندی</label>
              <select
                id="rsv-category"
                className="select-input"
                value={m.category}
                onChange={(e) => setField('category', e.target.value)}
                disabled={flags.metadataLocked}
              >
                <option value="Headquarters">ستاد</option>
                <option value="Project">پروژه</option>
              </select>
            </div>

            {m.category === 'Project' && (
              <div className="field">
                <label htmlFor="rsv-project">
                  پروژه <span className="required-mark" aria-hidden="true">*</span>
                </label>
                <select
                  id="rsv-project"
                  className="select-input"
                  value={m.projectId}
                  onChange={(e) => setField('projectId', e.target.value)}
                  disabled={flags.metadataLocked}
                >
                  <option value="">— انتخاب کنید —</option>
                  {sortedByTitle(lookups.projects).map((p) => (
                    <option key={p.key} value={p.key}>
                      {p.title}
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div className="field">
              <label htmlFor="rsv-management">
                مدیریت سازمانی <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <select
                id="rsv-management"
                className="select-input"
                value={m.managementId}
                onChange={(e) => {
                  setField('managementId', e.target.value)
                  setField('activityId', '') // فعالیت وابسته به مدیریت است؛ با تغییرش انتخابِ قبلی معتبر نیست.
                }}
                disabled={flags.metadataLocked}
              >
                <option value="">— انتخاب کنید —</option>
                {sortedByTitle(lookups.managements).map((mgmt) => (
                  <option key={mgmt.key} value={mgmt.key}>
                    {mgmt.title}
                  </option>
                ))}
              </select>
            </div>

            <div className="field">
              <label htmlFor="rsv-activity">
                فعالیت سازمانی <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <select
                id="rsv-activity"
                className="select-input"
                value={m.activityId}
                onChange={(e) => setField('activityId', e.target.value)}
                disabled={flags.metadataLocked || !m.managementId}
              >
                <option value="">{m.managementId ? '— انتخاب کنید —' : 'اول مدیریت را انتخاب کنید'}</option>
                {activitiesForManagement.map((a) => (
                  <option key={a.key} value={a.key}>
                    {a.title}
                  </option>
                ))}
              </select>
            </div>

            <div className="field">
              <label htmlFor="rsv-type">
                نوع سند <span className="required-mark" aria-hidden="true">*</span>
              </label>
              <select
                id="rsv-type"
                className="select-input"
                value={m.documentTypeId}
                onChange={(e) => setField('documentTypeId', e.target.value)}
                disabled={flags.metadataLocked}
              >
                <option value="">— انتخاب کنید —</option>
                {sortedByTitle(lookups.documentTypes).map((t) => (
                  <option key={t.key} value={t.key}>
                    {t.title}
                  </option>
                ))}
              </select>
            </div>

            <div className="field reserve-checkbox">
              <label>
                <input
                  type="checkbox"
                  checked={m.isEnglishVersion}
                  onChange={(e) => setField('isEnglishVersion', e.target.checked)}
                  disabled={flags.metadataLocked}
                />
                نسخه‌ی انگلیسی «‎(EN)‎»
              </label>
            </div>
          </div>

          {/* فیلدِ فایل: در مرحله‌ی رزرو اختیاری، برای نهایی‌سازی الزامی. */}
          <div className="field">
            <label htmlFor="rsv-file">
              فایل مدرک{' '}
              {flags.fileRequired ? (
                <span className="required-mark" aria-hidden="true">*</span>
              ) : (
                <span className="field-hint">(اختیاری در این مرحله)</span>
              )}
            </label>
            <label htmlFor="rsv-file" className={`file-drop ${state.file ? 'has-file' : ''}`}>
              <input
                id="rsv-file"
                type="file"
                className="file-drop__input"
                onChange={(e) => dispatch({ type: 'FILE_SELECTED', file: e.target.files?.[0] ?? null })}
                disabled={flags.isBusy}
              />
              <span className="file-drop__icon" aria-hidden="true">
                <UploadIcon size={20} />
              </span>
              {state.file ? (
                <div className="file-drop__selected">
                  <span className="file-drop__name mono">{state.file.name}</span>
                  <span className="file-drop__size">{formatFileSize(state.file.size)}</span>
                </div>
              ) : (
                <div className="file-drop__prompt">
                  <strong>فایل را انتخاب کنید</strong>
                  <span>می‌توانید این مرحله را رد کنید و فقط کد را رزرو کنید</span>
                </div>
              )}
            </label>
          </div>

          {state.error && (
            <div className="form-error" role="alert">
              {state.error}
            </div>
          )}

          <div className="create-panel__actions">
            {state.status === 'draft' ? (
              <button type="submit" className="btn btn-primary" disabled={!flags.canReserve || flags.isBusy}>
                {flags.isBusy ? 'در حال رزرو…' : 'رزرو کد'}
              </button>
            ) : (
              <>
                <button type="button" className="btn btn-ghost" onClick={() => navigate('/documents')}>
                  بعداً بارگذاری می‌کنم
                </button>
                <button type="submit" className="btn btn-primary" disabled={!flags.canFinalize || flags.isBusy}>
                  {flags.isBusy ? 'در حال بارگذاری…' : 'نهایی‌سازی و بارگذاری'}
                </button>
              </>
            )}
          </div>
        </form>
      )}

      {state.status === 'done' && (
        <form className="create-panel">
          <h2>مدرک نهایی شد</h2>
          <p>
            کد <span className="mono">{state.reservedCode}</span> با فایل ثبت و منتشر شد.
          </p>
          <div className="create-panel__actions">
            <button type="button" className="btn btn-ghost" onClick={() => dispatch({ type: 'RESET' })}>
              رزرو یک کد دیگر
            </button>
            <button type="button" className="btn btn-primary" onClick={() => navigate('/documents')}>
              رفتن به فهرست اسناد
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
