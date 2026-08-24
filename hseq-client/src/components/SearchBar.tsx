// جستجوی جمع‌وجورِ نوار بالا - جایگزین صفحه‌ی «جستجوی پیشرفته».
//
// خودِ تعریف فیلترها و تبدیلشان به پارامترهای آدرس در lib/searchFilters است تا فهرست
// اسناد هم بدون وابستگی به این کامپوننت از همان قرارداد استفاده کند.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { masterDataApi } from '../api/masterDataApi'
import { toPersianDigits } from '../lib/digits'
import { sortedByTitle } from '../lib/sorting'
import {
  EMPTY_FILTERS,
  countPanelFilters,
  filtersToParams,
  hasAnyFilter,
  readFilters,
  type SearchFilters,
} from '../lib/searchFilters'
import type { OrganizationalActivityLookup, OrganizationalManagementLookup } from '../types/api'
import { CloseIcon, FilterIcon, SearchIcon } from './Icons'

export function SearchBar() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const applied = useMemo(() => readFilters(searchParams), [searchParams])

  // پیش‌نویسِ داخل کادر و پنل. تا وقتی «جستجو» زده نشده، آدرس دست‌نخورده می‌ماند.
  const [draft, setDraft] = useState<SearchFilters>(applied)
  const [isPanelOpen, setPanelOpen] = useState(false)

  const [managements, setManagements] = useState<OrganizationalManagementLookup[]>([])
  const [activities, setActivities] = useState<OrganizationalActivityLookup[]>([])
  const [lookupError, setLookupError] = useState<string | null>(null)
  const lookupsRequested = useRef(false)

  const wrapRef = useRef<HTMLDivElement>(null)

  // آدرس که عوض شد (بازگشت مرورگر، لینک مستقیم، پاک کردن یک تراشه)، پیش‌نویس هم
  // باید همان را نشان بدهد.
  useEffect(() => {
    setDraft(applied)
  }, [applied])

  // داده‌های پایه فقط یک‌بار و فقط وقتی پنل واقعاً باز شد بارگذاری می‌شوند - نه در هر
  // بار بالا آمدن برنامه.
  useEffect(() => {
    if (!isPanelOpen || lookupsRequested.current) return
    lookupsRequested.current = true
    Promise.all([masterDataApi.getManagements(), masterDataApi.getActivities()])
      .then(([m, a]) => {
        setManagements(m)
        setActivities(a)
      })
      .catch(() => setLookupError('امکان بارگذاری مدیریت‌ها و فعالیت‌ها وجود ندارد.'))
  }, [isPanelOpen])

  // بستن پنل با کلیک بیرون یا Escape - رفتار متعارف هر پنل شناور.
  useEffect(() => {
    if (!isPanelOpen) return
    function onPointerDown(event: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(event.target as Node)) setPanelOpen(false)
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') setPanelOpen(false)
    }
    document.addEventListener('mousedown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [isPanelOpen])

  // فعالیت‌ها زیرمجموعه‌ی مدیریت‌اند و عنوان‌هایشان بین مدیریت‌های مختلف تکرار می‌شود.
  // برای همین تا مدیریت انتخاب نشده، فهرست خالی و انتخابگر غیرفعال است - وگرنه کاربر
  // فهرستی از عنوان‌های تکراری می‌دید که از رویشان معلوم نبود کدام مالِ کدام مدیریت است.
  const visibleActivities = useMemo(
    () =>
      draft.managementId
        ? sortedByTitle(activities.filter((a) => a.organizationalManagementId === draft.managementId))
        : [],
    [activities, draft.managementId],
  )

  // فهرست مدیریت‌ها الفبایی مرتب می‌شود؛ ترتیبِ سرور بر اساس کد است و حالا که کد آخرِ
  // برچسب می‌آید، آن ترتیب روی صفحه بی‌قاعده به نظر می‌رسید.
  const sortedManagements = useMemo(() => sortedByTitle(managements), [managements])

  const submit = useCallback(
    (next: SearchFilters) => {
      const params = filtersToParams(next)
      const queryString = params.toString()
      navigate(queryString ? `/documents?${queryString}` : '/documents')
      setPanelOpen(false)
    },
    [navigate],
  )

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    submit(draft)
  }

  function handleClearAll() {
    setDraft(EMPTY_FILTERS)
    submit(EMPTY_FILTERS)
  }

  // تغییر مدیریت، فعالیتِ انتخاب‌شده‌ی زیرمجموعه‌ی مدیریت قبلی را نامعتبر می‌کند.
  function handleManagementChange(managementId: string) {
    setDraft((prev) => ({ ...prev, managementId, activityId: '' }))
  }

  const panelFilterCount = countPanelFilters(draft)
  const appliedCount = countPanelFilters(applied)

  return (
    <div className="searchbar" ref={wrapRef}>
      <form className="searchbar__form" onSubmit={handleSubmit} role="search">
        <SearchIcon size={16} />
        <input
          type="search"
          className="searchbar__input"
          value={draft.query}
          onChange={(event) => setDraft((prev) => ({ ...prev, query: event.target.value }))}
          placeholder="جستجو در شماره یا نام سند..."
          aria-label="جستجو در شماره یا نام سند"
        />

        {/* پاک کردن سریع کل جستجو، فقط وقتی چیزی برای پاک کردن هست. */}
        {hasAnyFilter(applied) && (
          <button type="button" className="searchbar__clear" onClick={handleClearAll} aria-label="پاک کردن جستجو">
            <CloseIcon size={14} />
          </button>
        )}

        {/* دکمه‌ی فیلترها با نشانِ تعداد - کاربر بدون باز کردن پنل هم می‌فهمد فیلتری فعال است. */}
        <button
          type="button"
          className={`searchbar__filter-btn${isPanelOpen ? ' is-open' : ''}${appliedCount > 0 ? ' has-filters' : ''}`}
          onClick={() => setPanelOpen((open) => !open)}
          aria-expanded={isPanelOpen}
          aria-label="فیلترهای جستجو"
        >
          <FilterIcon size={15} />
          <span className="searchbar__filter-text">فیلتر</span>
          {appliedCount > 0 && <span className="searchbar__badge">{toPersianDigits(appliedCount)}</span>}
        </button>
      </form>

      {isPanelOpen && (
        <div className="search-panel" role="dialog" aria-label="فیلترهای جستجو">
          <div className="search-panel__grid">
            {/* مدیریت سازمانی */}
            <label className="search-panel__field">
              <span>مدیریت سازمانی</span>
              <select
                className="select-input"
                value={draft.managementId}
                onChange={(event) => handleManagementChange(event.target.value)}
              >
                <option value="">همه</option>
                {sortedManagements.map((m) => (
                  <option key={m.key} value={m.key}>
                    {m.title} ({toPersianDigits(m.code)})
                  </option>
                ))}
              </select>
            </label>

            {/* فعالیت سازمانی - تا مدیریت بالا انتخاب نشود قفل است. */}
            <label className="search-panel__field">
              <span>فعالیت سازمانی</span>
              <select
                className="select-input"
                value={draft.activityId}
                disabled={!draft.managementId}
                onChange={(event) => setDraft((prev) => ({ ...prev, activityId: event.target.value }))}
              >
                <option value="">{draft.managementId ? 'همه' : 'ابتدا مدیریت سازمانی را انتخاب کنید'}</option>
                {visibleActivities.map((a) => (
                  <option key={a.key} value={a.key}>
                    {a.title} ({toPersianDigits(a.code)})
                  </option>
                ))}
              </select>
            </label>
          </div>

          {lookupError && <p className="search-panel__error">{lookupError}</p>}

          {/* دو کلید روشن/خاموش. برخلاف انتخابگرهای بالا، این‌ها رفتار جستجو را عوض
              می‌کنند نه دامنه‌ی آن، پس جدا و با توضیح کوتاه آمده‌اند. */}
          <div className="search-panel__switches">
            <label className="switch-row">
              <input
                type="checkbox"
                checked={draft.onlyLatest}
                onChange={(event) => setDraft((prev) => ({ ...prev, onlyLatest: event.target.checked }))}
              />
              <span className="switch-row__track" aria-hidden="true">
                <span className="switch-row__thumb" />
              </span>
              <span className="switch-row__text">
                <strong>فقط آخرین بازنگری</strong>
                <span>نسخه‌های منسوخ‌شده در نتیجه نمی‌آیند.</span>
              </span>
            </label>

            <label className="switch-row">
              <input
                type="checkbox"
                checked={draft.inFileContent}
                onChange={(event) => setDraft((prev) => ({ ...prev, inFileContent: event.target.checked }))}
              />
              <span className="switch-row__track" aria-hidden="true">
                <span className="switch-row__thumb" />
              </span>
              <span className="switch-row__text">
                <strong>جستجو در محتوای فایل</strong>
                <span>متن استخراج‌شده‌ی فایل هم جستجو می‌شود.</span>
              </span>
            </label>
          </div>

          <div className="search-panel__actions">
            <button
              type="button"
              className="btn btn-ghost btn-sm"
              onClick={() => setDraft({ ...EMPTY_FILTERS, query: draft.query })}
              disabled={panelFilterCount === 0}
            >
              پاک کردن فیلترها
            </button>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => submit(draft)}>
              اعمال جستجو
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
