import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { documentApi } from '../api/documentApi'
import { masterDataApi } from '../api/masterDataApi'
import { ApiError } from '../lib/httpClient'
import { documentVersionLabel } from '../types/api'
import type { DocumentDto } from '../types/api'
import { DocumentFormDrawer } from '../components/DocumentFormDrawer'
import type { DocumentLookups } from '../components/DocumentFormDrawer'
import { LoadingState, EmptyState, ErrorState } from '../components/StateViews'

const PAGE_SIZE = 10
// Single fetch of active documents, then client-side search/paging over that
// set - the paged API has no search parameter yet. See Known Limitations.
const FETCH_SIZE = 500

type DrawerState = { mode: 'add' } | { mode: 'edit'; document: DocumentDto } | null

export function DocumentsPage() {
  const { hasCapability } = useAuth()
  const canManage = hasCapability('documents:manage')

  const [documents, setDocuments] = useState<DocumentDto[] | null>(null)
  const [loadError, setLoadError] = useState<{ status?: number; message: string } | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const [lookups, setLookups] = useState<DocumentLookups | null>(null)
  const [drawer, setDrawer] = useState<DrawerState>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const loadDocuments = useCallback(() => {
    setIsLoading(true)
    setLoadError(null)
    documentApi
      .getPaged(1, FETCH_SIZE, includeInactive)
      .then((result) => setDocuments(result.items))
      .catch((err) => {
        if (err instanceof ApiError && err.status === 403) {
          setLoadError({ status: 403, message: 'شما مجوز مشاهده اسناد را ندارید.' })
        } else {
          setLoadError({ message: 'امکان بارگذاری اسناد وجود ندارد. لطفاً دوباره تلاش کنید.' })
        }
      })
      .finally(() => setIsLoading(false))
  }, [includeInactive])

  useEffect(() => {
    loadDocuments()
  }, [loadDocuments])

  useEffect(() => {
    setPage(1)
  }, [search, includeInactive])

  async function ensureLookupsLoaded() {
    if (lookups) return lookups
    const [projects, managements, activities, documentTypes] = await Promise.all([
      masterDataApi.getProjects(),
      masterDataApi.getManagements(),
      masterDataApi.getActivities(),
      masterDataApi.getDocumentTypes(),
    ])
    const loaded = { projects, managements, activities, documentTypes }
    setLookups(loaded)
    return loaded
  }

  async function openAddDrawer() {
    await ensureLookupsLoaded()
    setDrawer({ mode: 'add' })
  }

  async function openEditDrawer(document: DocumentDto) {
    await ensureLookupsLoaded()
    setDrawer({ mode: 'edit', document })
  }

  async function handleDelete(document: DocumentDto) {
    const confirmed = window.confirm(`سند ${document.number} غیرفعال شود؟ این سند دیگر در فهرست عادی اسناد نمایش داده نخواهد شد.`)
    if (!confirmed) return

    setPendingDeleteId(document.key)
    try {
      await documentApi.remove(document.key)
      loadDocuments()
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان غیرفعال کردن سند وجود ندارد.')
    } finally {
      setPendingDeleteId(null)
    }
  }

  const filtered = useMemo(() => {
    if (!documents) return []
    const term = search.trim().toLowerCase()
    if (!term) return documents
    return documents.filter(
      (d) =>
        d.number.toLowerCase().includes(term) ||
        d.name.toLowerCase().includes(term),
    )
  }, [documents, search])

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const pageItems = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>اسناد</h1>
          <p>مرور و مدیریت اسناد کنترل‌شده HSEQ.</p>
        </div>
      </div>

      <div className="card">
        <div className="toolbar">
          <input
            type="search"
            className="search-input"
            placeholder="جستجو بر اساس شماره یا نام..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <div className="toolbar-actions">
            <label className="inactive-toggle">
              <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />
              نمایش غیرفعال‌ها
            </label>
            {canManage && (
              <button type="button" className="btn btn-primary" onClick={openAddDrawer}>
                افزودن سند
              </button>
            )}
          </div>
        </div>

        {isLoading && <LoadingState title="در حال بارگذاری اسناد..." />}

        {!isLoading && loadError && (
          <ErrorState
            title={loadError.message}
            action={
              loadError.status !== 403 ? (
                <button type="button" className="btn btn-secondary btn-sm" onClick={loadDocuments}>
                  تلاش مجدد
                </button>
              ) : undefined
            }
          />
        )}

        {!isLoading && !loadError && filtered.length === 0 && (
          <EmptyState title="سندی یافت نشد." description={search ? 'عبارت جستجوی دیگری را امتحان کنید.' : undefined} />
        )}

        {!isLoading && !loadError && filtered.length > 0 && (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>شماره</th>
                    <th>نام</th>
                    <th>بازنگری</th>
                    <th>تاریخ بازبینی</th>
                    <th>وضعیت</th>
                    {canManage && <th>عملیات</th>}
                  </tr>
                </thead>
                <tbody>
                  {pageItems.map((doc) => (
                    <tr key={doc.key}>
                      <td className="mono">{doc.number}</td>
                      <td>{doc.name}</td>
                      <td>
                        {documentVersionLabel(doc.lastVersion)}
                        {doc.contentRevision ? String(doc.contentRevision).padStart(2, '0') : ''}
                      </td>
                      <td>{doc.currentReviewDate ? doc.currentReviewDate.slice(0, 10) : '—'}</td>
                      <td>
                        <span className={`badge ${doc.isActive ? 'badge-success' : 'badge-muted'}`}>
                          {doc.isActive ? 'فعال' : 'غیرفعال'}
                        </span>
                      </td>
                      {canManage && (
                        <td className="actions-cell">
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => openEditDrawer(doc)}>
                            ویرایش
                          </button>
                          {doc.isActive && (
                            <button
                              type="button"
                              className="btn btn-danger btn-sm"
                              onClick={() => handleDelete(doc)}
                              disabled={pendingDeleteId === doc.key}
                            >
                              {pendingDeleteId === doc.key ? 'در حال غیرفعال کردن...' : 'غیرفعال کردن'}
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="pagination">
              <span>{filtered.length} سند</span>
              <div className="pagination-controls">
                <button type="button" className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                  قبلی
                </button>
                <span>
                  صفحه {page} از {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  بعدی
                </button>
              </div>
            </div>
          </>
        )}
      </div>

      {drawer && lookups && (
        <DocumentFormDrawer
          mode={drawer.mode}
          document={drawer.mode === 'edit' ? drawer.document : undefined}
          lookups={lookups}
          onClose={() => setDrawer(null)}
          onSaved={() => {
            setDrawer(null)
            loadDocuments()
          }}
        />
      )}
    </div>
  )
}