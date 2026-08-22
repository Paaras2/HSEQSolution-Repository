import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { documentApi } from '../api/documentApi'
import { ApiError } from '../lib/httpClient'
import { documentVersionLabel } from '../types/api'
import { isoToJalaliText } from '../lib/jalali'
import type { DocumentDto } from '../types/api'
import { DocumentFormDrawer } from '../components/DocumentFormDrawer'
import { RevisionHistoryDrawer } from '../components/RevisionHistoryDrawer'
import { RelatedDocumentsDrawer } from '../components/RelatedDocumentsDrawer'
import { downloadDocumentFile, viewDocumentFile } from '../lib/documentFile'
import { LoadingState, EmptyState, ErrorState } from '../components/StateViews'

const PAGE_SIZE = 10
// Single fetch of active documents, then client-side search/paging over that
// set - the paged API has no search parameter yet. See Known Limitations.
const FETCH_SIZE = 500

// Creating a document lives on its own route (/documents/new), so only the
// row-scoped actions open as dialogs here.
type DrawerState =
  | { mode: 'edit'; document: DocumentDto }
  | { mode: 'revise'; document: DocumentDto }
  | null

export function DocumentsPage() {
  const { hasCapability } = useAuth()
  const canManage = hasCapability('documents:manage')

  const [documents, setDocuments] = useState<DocumentDto[] | null>(null)
  const [loadError, setLoadError] = useState<{ status?: number; message: string } | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const [drawer, setDrawer] = useState<DrawerState>(null)
  const [historyDocument, setHistoryDocument] = useState<DocumentDto | null>(null)
  const [relationsDocument, setRelationsDocument] = useState<DocumentDto | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)
  const [pendingFileId, setPendingFileId] = useState<string | null>(null)

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

  // Editing and revising never needed the master-data lookups - only the create
  // form did, and that now loads them itself on /documents/new.
  function openEditDrawer(document: DocumentDto) {
    setDrawer({ mode: 'edit', document })
  }

  function openReviseDrawer(document: DocumentDto) {
    setDrawer({ mode: 'revise', document })
  }

  // Viewing is available to every authenticated user, not just canManage - the
  // Download route is not Admin-restricted either.
  async function handleView(document: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(document.key)
    try {
      const opened = await viewDocumentFile(document.key)
      if (!opened) {
        window.alert('مرورگر از باز شدن پنجره جلوگیری کرد. لطفاً به‌جای مشاهده، دانلود کنید.')
      }
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان باز کردن فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  async function handleDownload(document: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(document.key)
    try {
      await downloadDocumentFile(document.key, document.fileName ?? document.number)
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان دانلود فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
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
              <Link to="/documents/new" className="btn btn-primary">
                افزودن سند
              </Link>
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
                    {/* ستون «شماره مدارک مرتبط» - شماره‌ی سمت مقابلِ هر ارتباط، از هر دو جهت. */}
                    <th>شماره مدارک مرتبط</th>
                    <th>تاریخ بازبینی</th>
                    <th>وضعیت</th>
                    {/* Always rendered: viewing a document's file is available to every
                        authenticated user, so this column is no longer manage-only. */}
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {pageItems.map((doc) => (
                    <tr key={doc.key}>
                      <td className="mono">
                        {doc.number}
                        {doc.relatedDocumentNumber && (
                          <span className="row-subtext">بازنگری از {doc.relatedDocumentNumber}</span>
                        )}
                      </td>
                      <td>{doc.name}</td>
                      <td>
                        {documentVersionLabel(doc.lastVersion)}
                        {doc.contentRevision ? String(doc.contentRevision).padStart(2, '0') : ''}
                      </td>
                      {/* با کلیک روی سلول، همان دیالوگ مدارک مرتبط باز می‌شود. */}
                      <td>
                        {doc.relatedDocumentNumbers.length === 0 ? (
                          '—'
                        ) : (
                          <button
                            type="button"
                            className="related-numbers"
                            onClick={() => setRelationsDocument(doc)}
                            title="مشاهده و مدیریت مدارک مرتبط"
                          >
                            {doc.relatedDocumentNumbers.map((number) => (
                              <span key={number} className="badge badge-muted mono">
                                {number}
                              </span>
                            ))}
                          </button>
                        )}
                      </td>
                      {/* نمایش شمسی؛ مقدار ذخیره‌شده در دیتابیس همچنان میلادی است. */}
                      <td>{isoToJalaliText(doc.currentReviewDate ?? '') || '—'}</td>
                      <td>
                        {/* A superseded revision is also inactive, so it has to be
                            checked first - otherwise history reads as "deleted". */}
                        {doc.isSuperseded ? (
                          <span className="badge badge-muted">منسوخ (بازنگری شده)</span>
                        ) : (
                          <span className={`badge ${doc.isActive ? 'badge-success' : 'badge-danger'}`}>
                            {doc.isActive ? 'فعال' : 'غیرفعال'}
                          </span>
                        )}
                      </td>
                      <td className="actions-cell">
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleView(doc)}
                          disabled={pendingFileId !== null}
                        >
                          {pendingFileId === doc.key ? 'در حال باز کردن...' : 'مشاهده'}
                        </button>
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleDownload(doc)}
                          disabled={pendingFileId !== null}
                        >
                          دانلود
                        </button>
                        {/* Only meaningful once the chain has more than one link: either
                            this revision superseded an earlier one, or it was superseded. */}
                        {(doc.relatedDocumentNumber || doc.isSuperseded) && (
                          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setHistoryDocument(doc)}>
                            تاریخچه
                          </button>
                        )}
                        {/* برخلاف «تاریخچه»، این همیشه نمایش داده می‌شود: نداشتن مدرک
                            مرتبط از روی ردیف معلوم نیست، و همین دکمه هم راه دیدن
                            مرتبط‌ها و هم راه افزودنشان است. */}
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => setRelationsDocument(doc)}>
                          مدارک مرتبط
                        </button>
                        {canManage && (
                          <>
                            <button type="button" className="btn btn-secondary btn-sm" onClick={() => openEditDrawer(doc)}>
                              ویرایش
                            </button>
                            {/* Revising a superseded revision would fork the chain, which
                                the server rejects - so don't offer it. */}
                            {doc.isActive && !doc.isSuperseded && (
                              <button type="button" className="btn btn-secondary btn-sm" onClick={() => openReviseDrawer(doc)}>
                                بازنگری
                              </button>
                            )}
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
                          </>
                        )}
                      </td>
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

      {drawer && (
        <DocumentFormDrawer
          // Remounts when the target changes, so the form's initial state is
          // re-derived from the new document instead of being kept from the old one.
          key={`${drawer.mode}-${drawer.document.key}`}
          mode={drawer.mode}
          document={drawer.document}
          onClose={() => setDrawer(null)}
          onSaved={() => {
            setDrawer(null)
            loadDocuments()
          }}
        />
      )}

      {historyDocument && (
        <RevisionHistoryDrawer document={historyDocument} onClose={() => setHistoryDocument(null)} />
      )}

      {relationsDocument && (
        <RelatedDocumentsDrawer
          // مثل درایور فرم بالا: تعویض مدرکِ هدف باید حالت داخلی فرم افزودن را از نو
          // بسازد، نه اینکه انتخاب مدرک قبلی را با خودش ببرد.
          key={relationsDocument.key}
          document={relationsDocument}
          onClose={() => setRelationsDocument(null)}
        />
      )}
    </div>
  )
}