import { useEffect, useState } from 'react'
import type { DragEvent, FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { documentApi } from '../api/documentApi'
import { masterDataApi } from '../api/masterDataApi'
import { ApiError } from '../lib/httpClient'
import type {
  DocumentCategory,
  DocumentTypeLookup,
  OrganizationalActivityLookup,
  OrganizationalManagementLookup,
  ProjectLookup,
} from '../types/api'
import { LoadingState, ErrorState } from '../components/StateViews'

export interface DocumentLookups {
  projects: ProjectLookup[]
  managements: OrganizationalManagementLookup[]
  activities: OrganizationalActivityLookup[]
  documentTypes: DocumentTypeLookup[]
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} بایت`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} کیلوبایت`
  return `${(bytes / (1024 * 1024)).toFixed(1)} مگابایت`
}

// Creating a document is a distinct destination rather than an overlay: it
// collects four master-data choices that permanently determine the Document
// Number, so it benefits from its own URL, working browser navigation, and room
// to show what that number will be built from. Editing and revising stay as
// dialogs - they are short, and they act on one specific row.
export function DocumentCreatePage() {
  const navigate = useNavigate()

  const [lookups, setLookups] = useState<DocumentLookups | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [isLoadingLookups, setIsLoadingLookups] = useState(true)

  const [name, setName] = useState('')
  // پیش‌فرض «ستاد» طبق درخواست - کاربر فقط با انتخاب صریح «پروژه» فیلد پروژه را می‌بیند.
  const [category, setCategory] = useState<DocumentCategory>('Headquarters')
  const [projectId, setProjectId] = useState('')
  const [managementId, setManagementId] = useState('')
  const [activityId, setActivityId] = useState('')
  const [documentTypeId, setDocumentTypeId] = useState('')
  const [formerReviewDate, setFormerReviewDate] = useState('')
  const [currentReviewDate, setCurrentReviewDate] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [isDraggingFile, setIsDraggingFile] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

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
        setLookupError(
          err instanceof ApiError ? err.message : 'امکان بارگذاری داده‌های پایه وجود ندارد.',
        ),
      )
      .finally(() => setIsLoadingLookups(false))
  }

  useEffect(loadLookups, [])

  const selectedProject = lookups?.projects.find((p) => p.key === projectId)
  const selectedManagement = lookups?.managements.find((m) => m.key === managementId)
  const selectedActivity = lookups?.activities.find((a) => a.key === activityId)
  const selectedType = lookups?.documentTypes.find((t) => t.key === documentTypeId)
  const activitiesForManagement =
    lookups?.activities.filter((a) => a.organizationalManagementId === managementId) ?? []

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
    if (category === 'Project' && !projectId) {
      setError('برای دسته‌بندی «پروژه»، انتخاب پروژه الزامی است.')
      return
    }
    if (!managementId || !activityId || !documentTypeId) {
      setError('مدیریت، فعالیت و نوع سند همگی الزامی هستند.')
      return
    }
    if (!file) {
      setError('هنگام افزودن سند، فایل الزامی است.')
      return
    }

    setIsSubmitting(true)
    setError(null)
    try {
      await documentApi.add({
        name: name.trim(),
        category,
        formerReviewDate: formerReviewDate || null,
        currentReviewDate: currentReviewDate || null,
        relatedDocumentId: null,
        file,
        projectId: category === 'Project' ? projectId : null,
        organizationalManagementId: managementId,
        organizationalActivityId: activityId,
        documentTypeId,
      })
      navigate('/documents')
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'امکان ذخیره سند وجود ندارد. لطفاً دوباره تلاش کنید.')
      setIsSubmitting(false)
    }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <Link to="/documents" className="page-back">
            <span aria-hidden="true">›</span> بازگشت به فهرست اسناد
          </Link>
          <h1>افزودن سند</h1>
          <p>پس از ذخیره، شماره سند تولید می‌شود و دیگر قابل تغییر نخواهد بود.</p>
        </div>
      </div>

      {isLoadingLookups && (
        <div className="card">
          <LoadingState title="در حال بارگذاری داده‌های پایه..." />
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

      {!isLoadingLookups && !lookupError && lookups && (
        <form onSubmit={handleSubmit}>
          <div className="create-layout">
            <div className="create-main">
              {error && (
                <div className="card form-error" role="alert">
                  {error}
                </div>
              )}

              <section className="card create-card">
                <h2 className="create-card__title">اطلاعات سند</h2>
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

              <section className="card create-card">
                <h2 className="create-card__title">دسته‌بندی سند</h2>
                <p className="create-card__hint">
                  ساختار شماره سند بر اساس این انتخاب فرق می‌کند. در حال حاضر ساختار کدگذاری «پروژه» تغییر نکرده و همان مورد قبلی است.
                </p>
                <div className="form-grid">
                  <label className="check-row">
                    <input
                      type="radio"
                      name="doc-category"
                      checked={category === 'Headquarters'}
                      onChange={() => setCategory('Headquarters')}
                      disabled={isSubmitting}
                    />
                    <span className="check-row__text">
                      <strong>ستاد</strong>
                      <span>بدون پروژه - ساختار O+AA+DD-SSS-R، مثل ASYFM-008-A</span>
                    </span>
                  </label>

                  <label className="check-row">
                    <input
                      type="radio"
                      name="doc-category"
                      checked={category === 'Project'}
                      onChange={() => setCategory('Project')}
                      disabled={isSubmitting}
                    />
                    <span className="check-row__text">
                      <strong>پروژه</strong>
                      <span>با پروژه - ساختار PPPP+O+AA+DD+SSS+R، مثل P008QHSBD153A03</span>
                    </span>
                  </label>
                </div>
              </section>

              <section className="card create-card">
                <h2 className="create-card__title">طبقه‌بندی سازمانی</h2>
                <p className="create-card__hint">
                  شماره سند از ترکیب این موارد ساخته می‌شود، بنابراین پس از ذخیره قابل تغییر نیستند.
                </p>
                <div className="form-grid">
                  {category === 'Project' && (
                    <div className="field">
                      <label htmlFor="doc-project">
                        پروژه <span className="required-mark" aria-hidden="true">*</span>
                      </label>
                      <select
                        id="doc-project"
                        className="select-input"
                        value={projectId}
                        onChange={(e) => setProjectId(e.target.value)}
                        disabled={isSubmitting}
                      >
                        <option value="">یک پروژه انتخاب کنید…</option>
                        {lookups.projects.map((p) => (
                          <option key={p.key} value={p.key}>
                            {p.title} ({p.code})
                          </option>
                        ))}
                      </select>
                    </div>
                  )}

                  <div className="field">
                    <label htmlFor="doc-type">
                      نوع سند <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <select
                      id="doc-type"
                      className="select-input"
                      value={documentTypeId}
                      onChange={(e) => setDocumentTypeId(e.target.value)}
                      disabled={isSubmitting}
                    >
                      <option value="">یک نوع انتخاب کنید…</option>
                      {lookups.documentTypes.map((t) => (
                        <option key={t.key} value={t.key}>
                          {t.title} ({t.code})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="field">
                    <label htmlFor="doc-management">
                      مدیریت سازمانی <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <select
                      id="doc-management"
                      className="select-input"
                      value={managementId}
                      onChange={(e) => {
                        setManagementId(e.target.value)
                        setActivityId('')
                      }}
                      disabled={isSubmitting}
                    >
                      <option value="">یک مدیریت انتخاب کنید…</option>
                      {lookups.managements.map((m) => (
                        <option key={m.key} value={m.key}>
                          {m.title} ({m.code})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="field">
                    <label htmlFor="doc-activity">
                      فعالیت سازمانی <span className="required-mark" aria-hidden="true">*</span>
                    </label>
                    <select
                      id="doc-activity"
                      className="select-input"
                      value={activityId}
                      onChange={(e) => setActivityId(e.target.value)}
                      disabled={isSubmitting || !managementId}
                    >
                      <option value="">
                        {managementId ? 'یک فعالیت انتخاب کنید…' : 'ابتدا مدیریت را انتخاب کنید'}
                      </option>
                      {activitiesForManagement.map((a) => (
                        <option key={a.key} value={a.key}>
                          {a.title} ({a.code})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              </section>

              <section className="card create-card">
                <h2 className="create-card__title">تاریخ‌های بازبینی</h2>
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

              <section className="card create-card">
                <h2 className="create-card__title">
                  فایل سند <span className="required-mark" aria-hidden="true">*</span>
                </h2>

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
              </section>
            </div>

            <aside className="create-aside">
              <div className="card create-card">
                <h2 className="create-card__title">شماره سند</h2>
                <p className="create-card__hint">
                  با انتخاب هر مورد، بخش مربوط به آن پر می‌شود. سریال و بازنگری را سرور تعیین می‌کند.
                </p>

                <ol className="code-preview">
                  {category === 'Project' && (
                    <li className={selectedProject ? 'is-set' : ''}>
                      <span className="code-preview__value mono">{selectedProject?.code ?? 'PPPP'}</span>
                      <span className="code-preview__label">پروژه</span>
                    </li>
                  )}
                  <li className={selectedManagement ? 'is-set' : ''}>
                    <span className="code-preview__value mono">{selectedManagement?.code ?? 'O'}</span>
                    <span className="code-preview__label">مدیریت</span>
                  </li>
                  <li className={selectedActivity ? 'is-set' : ''}>
                    <span className="code-preview__value mono">{selectedActivity?.code ?? 'AA'}</span>
                    <span className="code-preview__label">فعالیت</span>
                  </li>
                  <li className={selectedType ? 'is-set' : ''}>
                    <span className="code-preview__value mono">{selectedType?.code ?? 'DD'}</span>
                    <span className="code-preview__label">نوع سند</span>
                  </li>
                  <li className="is-server-assigned">
                    <span className="code-preview__value mono">{category === 'Project' ? 'SSS' : '-SSS'}</span>
                    <span className="code-preview__label">سریال</span>
                  </li>
                  <li className="is-server-assigned">
                    <span className="code-preview__value mono">{category === 'Project' ? 'R' : '-R'}</span>
                    <span className="code-preview__label">بازنگری</span>
                  </li>
                </ol>
              </div>
            </aside>
          </div>

          <div className="page-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/documents')}
              disabled={isSubmitting}
            >
              انصراف
            </button>
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'در حال ذخیره…' : 'ذخیره سند'}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
