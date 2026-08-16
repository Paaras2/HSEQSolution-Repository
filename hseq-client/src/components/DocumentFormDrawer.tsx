import { useState } from 'react'
import type { FormEvent } from 'react'
import { documentApi } from '../api/documentApi'
import { ApiError } from '../lib/httpClient'
import type {
  DocumentDto,
  DocumentTypeLookup,
  OrganizationalActivityLookup,
  OrganizationalManagementLookup,
  ProjectLookup,
} from '../types/api'

export interface DocumentLookups {
  projects: ProjectLookup[]
  managements: OrganizationalManagementLookup[]
  activities: OrganizationalActivityLookup[]
  documentTypes: DocumentTypeLookup[]
}

interface DocumentFormDrawerProps {
  mode: 'add' | 'edit'
  document?: DocumentDto
  lookups: DocumentLookups
  onClose: () => void
  onSaved: () => void
}

// Add collects the master-data references the server needs to generate the
// Document Number (see HSEQ.Domain.DocumentNumbering) - the number itself is
// never entered or computed here. Edit only exposes what
// UpdateDocumentRequestModel actually accepts: Number/Project/organizational
// classification are immutable after creation, so those fields simply don't
// appear once a document exists.
export function DocumentFormDrawer({ mode, document, lookups, onClose, onSaved }: DocumentFormDrawerProps) {
  const [name, setName] = useState(document?.name ?? '')
  const [projectId, setProjectId] = useState('')
  const [managementId, setManagementId] = useState('')
  const [activityId, setActivityId] = useState('')
  const [documentTypeId, setDocumentTypeId] = useState('')
  const [formerReviewDate, setFormerReviewDate] = useState(document?.formerReviewDate?.slice(0, 10) ?? '')
  const [currentReviewDate, setCurrentReviewDate] = useState(document?.currentReviewDate?.slice(0, 10) ?? '')
  const [isActive, setIsActive] = useState(document?.isActive ?? true)
  const [file, setFile] = useState<File | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const activitiesForManagement = lookups.activities.filter((a) => a.organizationalManagementId === managementId)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (isSubmitting) return

    if (!name.trim()) {
      setError('نام الزامی است.')
      return
    }

    if (mode === 'add') {
      if (!projectId || !managementId || !activityId || !documentTypeId) {
        setError('پروژه، مدیریت، فعالیت و نوع سند همگی الزامی هستند.')
        return
      }
      if (!file) {
        setError('هنگام افزودن سند، فایل الزامی است.')
        return
      }
    }

    setIsSubmitting(true)
    setError(null)
    try {
      if (mode === 'add') {
        await documentApi.add({
          name: name.trim(),
          formerReviewDate: formerReviewDate || null,
          currentReviewDate: currentReviewDate || null,
          relatedDocumentId: null,
          file: file as File,
          projectId,
          organizationalManagementId: managementId,
          organizationalActivityId: activityId,
          documentTypeId,
        })
      } else if (document) {
        await documentApi.update({
          key: document.key,
          isActive,
          name: name.trim(),
          formerReviewDate: formerReviewDate || null,
          currentReviewDate: currentReviewDate || null,
          relatedDocumentId: document.relatedDocumentId,
          file,
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
    <div className="drawer-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="drawer-panel">
        <div className="drawer-header">
          <h2>{mode === 'add' ? 'افزودن سند' : `ویرایش ${document?.number}`}</h2>
          <button type="button" className="btn btn-ghost" onClick={onClose} aria-label="بستن">
            بستن
          </button>
        </div>

        <form id="document-form" onSubmit={handleSubmit}>
          <div className="drawer-body">
            {error && (
              <div className="form-error" role="alert">
                {error}
              </div>
            )}

            <div className="form-grid">
              <div className="field span-2">
                <label htmlFor="doc-name">نام</label>
                <input
                  id="doc-name"
                  className="text-input"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  disabled={isSubmitting}
                />
              </div>

              {mode === 'add' && (
                <>
                  <div className="field">
                    <label htmlFor="doc-project">پروژه</label>
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

                  <div className="field">
                    <label htmlFor="doc-type">نوع سند</label>
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
                    <label htmlFor="doc-management">مدیریت سازمانی</label>
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
                    <label htmlFor="doc-activity">فعالیت سازمانی</label>
                    <select
                      id="doc-activity"
                      className="select-input"
                      value={activityId}
                      onChange={(e) => setActivityId(e.target.value)}
                      disabled={isSubmitting || !managementId}
                    >
                      <option value="">{managementId ? 'یک فعالیت انتخاب کنید…' : 'ابتدا مدیریت را انتخاب کنید'}</option>
                      {activitiesForManagement.map((a) => (
                        <option key={a.key} value={a.key}>
                          {a.title} ({a.code})
                        </option>
                      ))}
                    </select>
                  </div>
                </>
              )}

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

              <div className="field span-2">
                <label htmlFor="doc-file">{mode === 'add' ? 'فایل' : 'جایگزینی فایل'}</label>
                <input
                  id="doc-file"
                  type="file"
                  className="text-input"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  disabled={isSubmitting}
                />
                {mode === 'edit' && (
                  <span className="hint">برای حفظ فایل فعلی ({document?.fileName ?? 'ندارد'})، این فیلد را خالی بگذارید.</span>
                )}
              </div>

              {mode === 'edit' && (
                <div className="field span-2">
                  <label>
                    <input
                      type="checkbox"
                      checked={isActive}
                      onChange={(e) => setIsActive(e.target.checked)}
                      disabled={isSubmitting}
                    />{' '}
                    فعال
                  </label>
                </div>
              )}
            </div>
          </div>

          <div className="drawer-footer">
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={isSubmitting}>
              انصراف
            </button>
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'در حال ذخیره…' : 'ذخیره'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}