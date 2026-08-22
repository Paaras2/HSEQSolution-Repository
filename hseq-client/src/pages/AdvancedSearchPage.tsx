import { useCallback, useEffect, useRef, useState } from 'react'
import { searchApi } from '../api/searchApi'
import { masterDataApi } from '../api/masterDataApi'
import { ApiError } from '../lib/httpClient'
import { downloadDocumentFile, viewDocumentFile } from '../lib/documentFile'
import { documentCategoryLabel, documentVersionLabel } from '../types/api'
import type {
  DocumentDto,
  DocumentSuggestion,
  DocumentTypeLookup,
  OrganizationalActivityLookup,
  OrganizationalManagementLookup,
  PagedResult,
  ProjectLookup,
  SearchDocumentsInput,
} from '../types/api'
import { DocumentPreviewModal } from '../components/DocumentPreviewModal'
import { RelatedDocumentsDrawer } from '../components/RelatedDocumentsDrawer'
import { LoadingState, EmptyState, ErrorState } from '../components/StateViews'

interface SearchLookups {
  projects: ProjectLookup[]
  managements: OrganizationalManagementLookup[]
  activities: OrganizationalActivityLookup[]
  documentTypes: DocumentTypeLookup[]
}

// «ذخیره‌ی آخرین جستجو» سمت مرورگر - نیازی به جدول یا endpoint جدید نیست، همون
// چیزی که کاربر آخرین بار جستجو کرده با بازگشت به این صفحه دوباره ظاهر می‌شود.
const LAST_SEARCH_STORAGE_KEY = 'hseq.lastSearch'
const PAGE_SIZE = 10

const DEFAULT_FILTERS: SearchDocumentsInput = {
  query: '',
  isActive: null,
  category: null,
  documentTypeId: null,
  organizationalManagementId: null,
  organizationalActivityId: null,
  projectId: null,
  onlyLatestRevision: false,
  searchInFileContent: false,
  pageNumber: 1,
  pageSize: PAGE_SIZE,
}

function loadSavedFilters(): SearchDocumentsInput {
  try {
    const raw = window.localStorage.getItem(LAST_SEARCH_STORAGE_KEY)
    if (!raw) return DEFAULT_FILTERS
    return { ...DEFAULT_FILTERS, ...(JSON.parse(raw) as Partial<SearchDocumentsInput>), pageNumber: 1 }
  } catch {
    return DEFAULT_FILTERS
  }
}

export function AdvancedSearchPage() {
  const [lookups, setLookups] = useState<SearchLookups | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)

  const [filters, setFilters] = useState<SearchDocumentsInput>(loadSavedFilters)
  const [results, setResults] = useState<PagedResult<DocumentDto> | null>(null)
  const [isSearching, setIsSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)
  const [hasSearchedOnce, setHasSearchedOnce] = useState(false)

  const [suggestions, setSuggestions] = useState<DocumentSuggestion[]>([])
  const [showSuggestions, setShowSuggestions] = useState(false)

  const [previewDocument, setPreviewDocument] = useState<DocumentDto | null>(null)
  const [relationsDocument, setRelationsDocument] = useState<DocumentDto | null>(null)
  const [pendingFileId, setPendingFileId] = useState<string | null>(null)
  const [isExporting, setIsExporting] = useState(false)

  const suggestTimer = useRef<number | null>(null)

  useEffect(() => {
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
  }, [])

  const runSearch = useCallback((toSearch: SearchDocumentsInput) => {
    setIsSearching(true)
    setSearchError(null)
    searchApi
      .search(toSearch)
      .then((result) => {
        setResults(result)
        setHasSearchedOnce(true)
        window.localStorage.setItem(LAST_SEARCH_STORAGE_KEY, JSON.stringify(toSearch))
      })
      .catch((err) => setSearchError(err instanceof ApiError ? err.message : 'امکان انجام جستجو وجود ندارد.'))
      .finally(() => setIsSearching(false))
  }, [])

  // پیدا کردن دقیق مدرک از همون لحظه‌ی ورود به صفحه شروع می‌شود: اگر جستجوی قبلی
  // ذخیره شده بود، همون فیلترها برمی‌گردند و نتیجه بلافاصله نشان داده می‌شود.
  useEffect(() => {
    runSearch(filters)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setShowSuggestions(false)
    runSearch({ ...filters, pageNumber: 1 })
  }

  function goToPage(pageNumber: number) {
    const next = { ...filters, pageNumber }
    setFilters(next)
    runSearch(next)
  }

  function updateFilter<K extends keyof SearchDocumentsInput>(key: K, value: SearchDocumentsInput[K]) {
    setFilters((prev) => {
      const next = { ...prev, [key]: value }
      // تغییر مدیریت سازمانی، فعالیت انتخاب‌شده‌ی زیرمجموعه‌ی مدیریت قبلی را نامعتبر می‌کند.
      if (key === 'organizationalManagementId') next.organizationalActivityId = null
      return next
    })
  }

  function handleQueryChange(value: string) {
    updateFilter('query', value)

    if (suggestTimer.current) window.clearTimeout(suggestTimer.current)
    if (value.trim().length < 2) {
      setSuggestions([])
      setShowSuggestions(false)
      return
    }

    suggestTimer.current = window.setTimeout(() => {
      searchApi
        .suggest(value)
        .then((items) => {
          setSuggestions(items)
          setShowSuggestions(items.length > 0)
        })
        .catch(() => {
          /* پیشنهاد خودکار صرفاً کمکی است - شکستش نباید به کاربر نشان داده شود. */
        })
    }, 300)
  }

  function handleSuggestionPick(suggestion: DocumentSuggestion) {
    setShowSuggestions(false)
    const next = { ...filters, query: suggestion.number, pageNumber: 1 }
    setFilters(next)
    runSearch(next)
  }

  function handleReset() {
    setFilters(DEFAULT_FILTERS)
    setSuggestions([])
    setShowSuggestions(false)
    runSearch(DEFAULT_FILTERS)
  }

  async function handleExport() {
    setIsExporting(true)
    try {
      const blob = await searchApi.exportExcel(filters)
      const url = URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = 'documents-export.xlsx'
      window.document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان دریافت خروجی اکسل وجود ندارد.')
    } finally {
      setIsExporting(false)
    }
  }

  async function handleView(doc: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(doc.key)
    try {
      const opened = await viewDocumentFile(doc.key)
      if (!opened) window.alert('مرورگر از باز شدن پنجره جلوگیری کرد. لطفاً به‌جای مشاهده، دانلود کنید.')
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان باز کردن فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  async function handleDownload(doc: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(doc.key)
    try {
      await downloadDocumentFile(doc.key, doc.fileName ?? doc.number)
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان دانلود فایل وجود ندارد.')
    } finally {
      setPendingFileId(null)
    }
  }

  const activitiesForManagement =
    lookups?.activities.filter((a) => a.organizationalManagementId === filters.organizationalManagementId) ?? []

  const totalPages = results ? Math.max(1, results.totalPages) : 1

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>جستجوی پیشرفته</h1>
          <p>پیدا کردن دقیق مدرک، تشخیص آخرین نسخه‌ی معتبر، و رفتن مستقیم به مشاهده/دانلود/بازنگری.</p>
        </div>
      </div>

      <form className="card create-card" onSubmit={handleSubmit}>
        <div className="field" style={{ position: 'relative' }}>
          <label htmlFor="search-query">جستجو بر اساس شماره یا عنوان</label>
          <input
            id="search-query"
            className="text-input"
            value={filters.query}
            onChange={(e) => handleQueryChange(e.target.value)}
            onFocus={() => setShowSuggestions(suggestions.length > 0)}
            onBlur={() => window.setTimeout(() => setShowSuggestions(false), 150)}
            placeholder="شماره یا بخشی از عنوان سند..."
            autoComplete="off"
          />
          {showSuggestions && (
            <ul className="suggestion-list">
              {suggestions.map((s) => (
                <li key={s.key}>
                  <button type="button" onMouseDown={() => handleSuggestionPick(s)}>
                    <span className="mono">{s.number}</span>
                    <span>{s.name}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        {lookupError && <div className="form-error">{lookupError}</div>}

        <div className="form-grid">
          <div className="field">
            <label htmlFor="search-status">وضعیت</label>
            <select
              id="search-status"
              className="select-input"
              value={filters.isActive === null ? '' : String(filters.isActive)}
              onChange={(e) => updateFilter('isActive', e.target.value === '' ? null : e.target.value === 'true')}
            >
              <option value="">همه</option>
              <option value="true">فعال</option>
              <option value="false">غیرفعال</option>
            </select>
          </div>

          <div className="field">
            <label htmlFor="search-category">دسته‌بندی سند</label>
            <select
              id="search-category"
              className="select-input"
              value={filters.category ?? ''}
              onChange={(e) => updateFilter('category', e.target.value === '' ? null : (e.target.value as SearchDocumentsInput['category']))}
            >
              <option value="">همه</option>
              <option value="Headquarters">ستاد</option>
              <option value="Project">پروژه</option>
            </select>
          </div>

          <div className="field">
            <label htmlFor="search-doctype">نوع سند</label>
            <select
              id="search-doctype"
              className="select-input"
              value={filters.documentTypeId ?? ''}
              onChange={(e) => updateFilter('documentTypeId', e.target.value || null)}
            >
              <option value="">همه</option>
              {lookups?.documentTypes.map((t) => (
                <option key={t.key} value={t.key}>
                  {t.title} ({t.code})
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="search-management">مدیریت سازمانی</label>
            <select
              id="search-management"
              className="select-input"
              value={filters.organizationalManagementId ?? ''}
              onChange={(e) => updateFilter('organizationalManagementId', e.target.value || null)}
            >
              <option value="">همه</option>
              {lookups?.managements.map((m) => (
                <option key={m.key} value={m.key}>
                  {m.title} ({m.code})
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="search-activity">فعالیت سازمانی</label>
            <select
              id="search-activity"
              className="select-input"
              value={filters.organizationalActivityId ?? ''}
              onChange={(e) => updateFilter('organizationalActivityId', e.target.value || null)}
              disabled={!filters.organizationalManagementId}
            >
              <option value="">{filters.organizationalManagementId ? 'همه' : 'ابتدا مدیریت را انتخاب کنید'}</option>
              {activitiesForManagement.map((a) => (
                <option key={a.key} value={a.key}>
                  {a.title} ({a.code})
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="search-project">نام پروژه</label>
            <select
              id="search-project"
              className="select-input"
              value={filters.projectId ?? ''}
              onChange={(e) => updateFilter('projectId', e.target.value || null)}
            >
              <option value="">همه</option>
              {lookups?.projects.map((p) => (
                <option key={p.key} value={p.key}>
                  {p.title} ({p.code})
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="toolbar-actions" style={{ marginTop: '4px' }}>
          <label className="inactive-toggle">
            <input
              type="checkbox"
              checked={filters.onlyLatestRevision}
              onChange={(e) => updateFilter('onlyLatestRevision', e.target.checked)}
            />
            فقط آخرین بازنگری
          </label>
          <label className="inactive-toggle">
            <input
              type="checkbox"
              checked={filters.searchInFileContent}
              onChange={(e) => updateFilter('searchInFileContent', e.target.checked)}
            />
            جستجو در محتوای فایل
          </label>
        </div>

        <div className="page-actions">
          <button type="button" className="btn btn-secondary" onClick={handleReset} disabled={isSearching}>
            پاک کردن فیلترها
          </button>
          <button type="button" className="btn btn-secondary" onClick={handleExport} disabled={isExporting || isSearching}>
            {isExporting ? 'در حال دریافت...' : 'خروجی اکسل'}
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSearching}>
            {isSearching ? 'در حال جستجو...' : 'جستجو'}
          </button>
        </div>
      </form>

      <div className="card" style={{ marginTop: '16px' }}>
        {isSearching && !hasSearchedOnce && <LoadingState title="در حال جستجو..." />}

        {searchError && (
          <ErrorState
            title={searchError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={() => runSearch(filters)}>
                تلاش مجدد
              </button>
            }
          />
        )}

        {!searchError && results && results.items.length === 0 && (
          <EmptyState title="سندی با این مشخصات یافت نشد." description="فیلترها یا عبارت جستجو را تغییر دهید." />
        )}

        {!searchError && results && results.items.length > 0 && (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>شماره</th>
                    <th>نام</th>
                    <th>دسته‌بندی</th>
                    <th>بازنگری</th>
                    {/* ستون «شماره مدارک مرتبط» - مثل فهرست اسناد، شماره‌ی سمت مقابلِ هر ارتباط. */}
                    <th>شماره مدارک مرتبط</th>
                    <th>وضعیت</th>
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {results.items.map((doc) => (
                    <tr key={doc.key}>
                      <td className="mono">
                        {doc.number}
                        {doc.relatedDocumentNumber && <span className="row-subtext">بازنگری از {doc.relatedDocumentNumber}</span>}
                      </td>
                      <td>{doc.name}</td>
                      <td>{documentCategoryLabel(doc.category)}</td>
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
                      <td>
                        {doc.isSuperseded ? (
                          <span className="badge badge-muted">منسوخ (بازنگری شده)</span>
                        ) : (
                          <span className={`badge ${doc.isActive ? 'badge-success' : 'badge-danger'}`}>
                            {doc.isActive ? 'فعال' : 'غیرفعال'}
                          </span>
                        )}
                      </td>
                      <td className="actions-cell">
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => setPreviewDocument(doc)}>
                          پیش‌نمایش
                        </button>
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleView(doc)}
                          disabled={pendingFileId !== null}
                        >
                          مشاهده
                        </button>
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => handleDownload(doc)}
                          disabled={pendingFileId !== null}
                        >
                          دانلود
                        </button>
                        {/* خواندن مدارک مرتبط برای هر کاربر احرازهویت‌شده باز است؛ فرم
                            افزودن را خودِ دیالوگ بر اساس دسترسی پنهان می‌کند، پس اینجا
                            شرط جداگانه‌ای لازم نیست. */}
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => setRelationsDocument(doc)}
                        >
                          مدارک مرتبط
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="pagination">
              <span>{results.totalCount} سند</span>
              <div className="pagination-controls">
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  disabled={filters.pageNumber <= 1 || isSearching}
                  onClick={() => goToPage(filters.pageNumber - 1)}
                >
                  قبلی
                </button>
                <span>
                  صفحه {filters.pageNumber} از {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  disabled={filters.pageNumber >= totalPages || isSearching}
                  onClick={() => goToPage(filters.pageNumber + 1)}
                >
                  بعدی
                </button>
              </div>
            </div>
          </>
        )}
      </div>

      {previewDocument && <DocumentPreviewModal document={previewDocument} onClose={() => setPreviewDocument(null)} />}

      {relationsDocument && (
        <RelatedDocumentsDrawer
          // تعویض مدرکِ هدف باید حالت داخلی فرم افزودن را از نو بسازد، نه اینکه انتخاب
          // مدرک قبلی را با خودش ببرد - مثل همین الگو در صفحه‌ی اسناد.
          key={relationsDocument.key}
          document={relationsDocument}
          onClose={() => setRelationsDocument(null)}
        />
      )}
    </div>
  )
}
