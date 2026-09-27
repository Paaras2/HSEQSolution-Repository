import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { documentApi } from '../api/documentApi'
import { searchApi } from '../api/searchApi'
import { ApiError } from '../lib/httpClient'
import { documentVersionLabel } from '../types/api'
import { isoToJalaliText } from '../lib/jalali'
import { toPersianDigits } from '../lib/digits'
import type { DocumentDto } from '../types/api'
import { DocumentFormDrawer } from '../components/DocumentFormDrawer'
import { RevisionHistoryDrawer } from '../components/RevisionHistoryDrawer'
import { RelatedDocumentsDrawer } from '../components/RelatedDocumentsDrawer'
import { DocumentPreviewModal } from '../components/DocumentPreviewModal'
import { ExportColumnsDialog } from '../components/ExportColumnsDialog'
import { DocumentTreeView } from '../components/DocumentTreeView'
import type { DocumentTreeLabels } from '../components/DocumentTreeView'
import { ViewSwitch } from '../components/ViewSwitch'
import type { ViewSwitchOption } from '../components/ViewSwitch'
import { masterDataApi } from '../api/masterDataApi'
import { downloadDocumentFile, viewDocumentFile } from '../lib/documentFile'
import { LoadingState, EmptyState, ErrorState } from '../components/StateViews'
import { SEARCH_PARAM, hasAnyFilter, readFilters } from '../lib/searchFilters'
import { IconButton } from '../components/IconButton'
import {
  CloseIcon,
  DeactivateIcon,
  DownloadIcon,
  EditIcon,
  EyeIcon,
  HistoryIcon,
  ReviseIcon,
  SearchIcon,
  SpinnerIcon,
  TableIcon,
  TreeIcon,
} from '../components/Icons'

const PAGE_SIZE = 10

// سقفِ تعداد سندی که نمای درختی یک‌جا می‌گیرد. درخت صفحه‌بندی ندارد (دسته‌بندی با
// صفحه‌بندی معنا نمی‌دهد)، پس یک سقف لازم است تا یک درخواستِ بی‌فیلتر کلِ آرشیو را
// نکشد؛ اگر نتیجه از این بیشتر بود، بالای درخت گفته می‌شود.
const TREE_PAGE_SIZE = 1000

type ViewMode = 'table' | 'tree'

// گزینه‌های نما - بیرون از کامپوننت، چون ثابت‌اند و نباید در هر رندر دوباره ساخته شوند.
const VIEW_OPTIONS: ViewSwitchOption<ViewMode>[] = [
  { value: 'table', label: 'جدولی', icon: <TableIcon size={16} />, hint: 'فهرست ردیفی با همه‌ی ستون‌ها و عملیات' },
  { value: 'tree', label: 'درختی', icon: <TreeIcon size={16} />, hint: 'گروه‌بندی بر پایه‌ی مدیریت و فعالیت سازمانی' },
]

// Creating a document lives on its own route (/documents/new), so only the
// row-scoped actions open as dialogs here.
//
// «ویرایش» و «بازنگری» یک دیالوگ شدند (DocumentFormDrawer تصمیم می‌گیرد کدام
// عملیات واقعی رخ دهد، بر پایه‌ی اینکه کاربر فایل پیوست کرده یا نه)، پس این‌جا هم
// فقط یک نوع باز است، نه دو حالت جدا.
type DrawerState = DocumentDto | null

// وضعیت عملیات گروهی: چون API نسخه‌ی دسته‌ای ندارد، درخواست‌ها یکی‌یکی فرستاده
// می‌شوند و همین شمارنده پیشرفت را روی نوار انتخاب نشان می‌دهد.
type BulkState = { kind: 'download' | 'delete'; done: number; total: number } | null

// چک‌باکس‌های کنترل‌شده‌ای که منطقشان روی click است، هنوز باید onChange داشته باشند
// وگرنه React آن‌ها را فقط-خواندنی می‌داند و هشدار می‌دهد.
function noop() {}

export function DocumentsPage() {
  const { hasCapability } = useAuth()
  const canManage = hasCapability('documents:manage')
  // خروجی اکسل شاملِ داده‌ی خامِ کلِ آرشیو است، پس فقط Admin و DocumentManager -
  // سرور هم همین را اجبار می‌کند (۴۰۳ برای بقیه)؛ این‌جا فقط دکمه را زودتر پنهان می‌کند.
  const canExport = hasCapability('documents:export')

  const [documents, setDocuments] = useState<DocumentDto[] | null>(null)
  const [totalCount, setTotalCount] = useState(0)
  const [loadError, setLoadError] = useState<{ status?: number; message: string } | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [includeInactive, setIncludeInactive] = useState(false)

  // «نمایش آرشیو» فقط برای کسی معنا دارد که می‌تواند سند را آرشیو/بازگردانی کند.
  // برای نقش «فقط مشاهده» نه کلیدش رندر می‌شود و نه مقدارش اثر می‌گذارد؛ دومی مهم
  // است تا اگر نقشِ همین نشست عوض شد، فهرست روی حالتِ قبلی جا نماند.
  const showInactive = canManage && includeInactive
  const [isExporting, setIsExporting] = useState(false)
  // کلیک روی «خروجی اکسل» اول این دیالوگ را باز می‌کند، نه خودِ دانلود را؛ خروجیِ
  // واقعی از داخلِ دیالوگ با ستون‌های انتخاب‌شده انجام می‌شود.
  const [isExportDialogOpen, setIsExportDialogOpen] = useState(false)

  // نوار جستجوی بالای صفحه فیلترها را در آدرس می‌نویسد و این صفحه فقط آن‌ها را
  // می‌خواند - یعنی بازگشت مرورگر، تازه‌سازی و اشتراک لینکِ جستجو رایگان به دست می‌آید.
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const filters = useMemo(() => readFilters(searchParams), [searchParams])
  const isSearching = hasAnyFilter(filters)
  const [page, setPage] = useState(1)

  // برداشتن یک فیلتر از روی تراشه‌های بالای جدول؛ بقیه‌ی پارامترها دست‌نخورده می‌مانند.
  // استثنا: فعالیت سازمانی زیرمجموعه‌ی مدیریت است، پس با رفتن مدیریت باید آن هم برود -
  // وگرنه فیلتری باقی می‌ماند که در پنل جستجو دیگر قابل دیدن و تغییر نیست.
  function clearParam(name: string) {
    const next = new URLSearchParams(searchParams)
    next.delete(name)
    if (name === SEARCH_PARAM.management) next.delete(SEARCH_PARAM.activity)
    setSearchParams(next, { replace: true })
  }

  function clearAllParams() {
    setSearchParams(new URLSearchParams(), { replace: true })
  }

  const [drawer, setDrawer] = useState<DrawerState>(null)
  const [historyDocument, setHistoryDocument] = useState<DocumentDto | null>(null)
  const [relationsDocument, setRelationsDocument] = useState<DocumentDto | null>(null)
  // وقتی مرورگر باز شدن تب را بلاک می‌کند، فایل همین‌جا داخل برنامه نشان داده می‌شود.
  const [previewDocument, setPreviewDocument] = useState<DocumentDto | null>(null)

  // نمای جدولی (پیش‌فرض) یا نمای درختی. نمای درختی همان نتیجه‌ی جاری را بر پایه‌ی
  // «مدیریت سازمانی ← فعالیت سازمانی» گروه‌بندی می‌کند؛ کد و عنوانِ گروه‌ها از اطلاعات
  // پایه می‌آید و خودِ گروه‌بندی از فیلدهای همان سند.
  // نمای درختی پیش‌فرض است: دسته‌بندیِ مدیریت ← فعالیت برای مرور اسناد گویاتر از
  // فهرستِ تخت است؛ نمای جدولی همچنان یک کلیک آن‌طرف‌تر می‌ماند.
  const [viewMode, setViewMode] = useState<ViewMode>('tree')
  const isTreeView = viewMode === 'tree'
  const [treeLabels, setTreeLabels] = useState<DocumentTreeLabels>({
    managements: new Map(),
    activities: new Map(),
  })
  useEffect(() => {
    Promise.all([masterDataApi.getManagements(), masterDataApi.getActivities()])
      .then(([managements, activities]) => {
        setTreeLabels({
          managements: new Map(managements.map((m) => [m.key, { code: m.code, title: m.title }])),
          activities: new Map(activities.map((a) => [a.key, { code: a.code, title: a.title }])),
        })
      })
      .catch(noop)
  }, [])
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)
  const [pendingFileId, setPendingFileId] = useState<string | null>(null)

  // انتخاب چندتایی: فقط کلید اسناد نگه داشته می‌شود تا با هر بار بارگذاری مجدد،
  // داده‌ی کهنه‌ی ردیف در حافظه نماند.
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(() => new Set())
  const [bulk, setBulk] = useState<BulkState>(null)
  // چک‌باکس سرستون سه حالت دارد؛ حالت «نیمه‌انتخاب» فقط از طریق DOM قابل تنظیم است.
  const selectAllRef = useRef<HTMLInputElement>(null)
  // آخرین ردیفِ کلیک‌شده، برای انتخاب بازه‌ای با نگه‌داشتن Shift.
  const lastToggledKeyRef = useRef<string | null>(null)

  // ورودیِ سرویس جستجو. فهرست بدون فیلتر هم از همین مسیر می‌آید تا یک راهِ داده
  // داشته باشیم؛ «نمایش آرشیو» دقیقاً روی IsActive می‌نشیند (نال یعنی همه).
  const searchInput = useMemo(
    () => ({
      query: filters.query,
      isActive: showInactive ? null : true,
      category: null,
      documentTypeId: null,
      organizationalManagementId: filters.managementId || null,
      organizationalActivityId: filters.activityId || null,
      projectId: null,
      onlyLatestRevision: filters.onlyLatest,
      searchInFileContent: filters.inFileContent,
      // درخت باید کلِ نتیجه‌ی فیلترشده را ببیند، وگرنه شاخه‌هایش فقط همان ده ردیفِ
      // صفحه‌ی جاری را دسته‌بندی می‌کنند و دسته‌بندی بی‌معنا می‌شود. جدول همان
      // صفحه‌بندی سمت سرور را نگه می‌دارد.
      pageNumber: isTreeView ? 1 : page,
      pageSize: isTreeView ? TREE_PAGE_SIZE : PAGE_SIZE,
    }),
    [filters, showInactive, page, isTreeView],
  )

  const loadDocuments = useCallback(() => {
    setIsLoading(true)
    setLoadError(null)
    searchApi
      .search(searchInput)
      .then((result) => {
        setDocuments(result.items)
        setTotalCount(result.totalCount)
      })
      .catch((err) => {
        if (err instanceof ApiError && err.status === 403) {
          setLoadError({ status: 403, message: 'شما مجوز مشاهده اسناد را ندارید.' })
        } else {
          setLoadError({ message: 'امکان بارگذاری اسناد وجود ندارد. لطفاً دوباره تلاش کنید.' })
        }
      })
      .finally(() => setIsLoading(false))
  }, [searchInput])

  useEffect(() => {
    loadDocuments()
  }, [loadDocuments])

  useEffect(() => {
    setPage(1)
    // با تغییر فیلتر، ردیف‌های انتخاب‌شده ممکن است دیگر در فهرست نباشند؛ انتخابِ
    // نامرئی خطرناک است، پس پاک می‌شود.
    setSelectedKeys(new Set())
  }, [filters, showInactive])

  // با صفحه‌بندی سمت سرور، ردیف‌های صفحه‌ی قبل دیگر در حافظه نیستند؛ نگه داشتن
  // انتخابشان فقط شمارنده‌ی داک را گمراه‌کننده می‌کرد. عوض شدنِ نما هم همین حکم را
  // دارد: نمای درختی چک‌باکس ندارد، پس انتخابِ نامرئی باقی نمی‌ماند.
  useEffect(() => {
    setSelectedKeys(new Set())
  }, [page, viewMode])

  // خروجی اکسل، همان فیلترهای جاری را به سرور می‌دهد - قابلیتی که پیش‌تر روی صفحه‌ی
  // «جستجوی پیشرفته» بود و با حذف آن صفحه به اینجا منتقل شد.
  async function handleExport(columns: string[]) {
    if (isExporting) return
    setIsExporting(true)
    try {
      const blob = await searchApi.exportExcel(searchInput, columns)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'documents-export.xlsx'
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
      setIsExportDialogOpen(false)
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان تهیه‌ی خروجی اکسل وجود ندارد.')
    } finally {
      setIsExporting(false)
    }
  }

  // Editing and revising never needed the master-data lookups - only the create
  // form did, and that now loads them itself on /documents/new.
  function openFormDrawer(document: DocumentDto) {
    setDrawer(document)
  }

  // Viewing is available to every authenticated user, not just canManage - the
  // Download route is not Admin-restricted either.
  async function handleView(document: DocumentDto) {
    if (pendingFileId) return
    setPendingFileId(document.key)
    try {
      const opened = await viewDocumentFile(document.key)
      // هشدار حذف شد: بلاک‌شدن پاپ‌آپ تقصیر کاربر نیست و پیامش هم کاری از پیش نمی‌برد.
      // به‌جایش همان فایل در پیش‌نمایش درون‌برنامه‌ای باز می‌شود.
      if (!opened) setPreviewDocument(document)
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
    const confirmed = window.confirm(`سند ${toPersianDigits(document.number)} آرشیو شود؟ این سند دیگر در فهرست عادی اسناد نمایش داده نخواهد شد.`)
    if (!confirmed) return

    setPendingDeleteId(document.key)
    try {
      await documentApi.remove(document.key)
      loadDocuments()
    } catch (err) {
      window.alert(err instanceof ApiError ? err.message : 'امکان آرشیو کردن سند وجود ندارد.')
    } finally {
      setPendingDeleteId(null)
    }
  }

  // صفحه‌بندی سمت سرور است، پس ردیف‌های همین صفحه همان چیزی است که آمده. مموایز
  // می‌شود تا آرایه‌ی تازه در هر رندر، محاسبات پایین‌دستی را بی‌اثر نکند.
  const pageItems = useMemo(() => documents ?? [], [documents])
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  // انتخاب فقط روی ردیف‌های بارگذاری‌شده معنا دارد: با صفحه‌بندی سمت سرور، ردیف‌های
  // صفحه‌های دیگر اصلاً در حافظه نیستند.
  const selectedDocuments = useMemo(
    () => pageItems.filter((d) => selectedKeys.has(d.key)),
    [pageItems, selectedKeys],
  )
  // فقط سند فعال قابل آرشیو کردن است؛ همین عدد روی دکمه‌ی داک هم نشان داده می‌شود.
  const selectedActiveCount = selectedDocuments.filter((d) => d.isActive).length
  const allPageSelected = pageItems.length > 0 && pageItems.every((d) => selectedKeys.has(d.key))
  const somePageSelected = pageItems.some((d) => selectedKeys.has(d.key))

  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = !allPageSelected && somePageSelected
    }
  }, [allPageSelected, somePageSelected])

  // انتخاب/لغو یک ردیف. با نگه‌داشتن Shift، همه‌ی ردیف‌های بین آخرین انتخاب و ردیف
  // فعلی (در همین صفحه) هم‌وضعیت می‌شوند - رفتار متعارف جدول‌های چندانتخابی.
  function toggleRow(key: string, checked: boolean, withRange: boolean) {
    // لنگرِ بازه باید همین‌جا خوانده شود، نه داخل به‌روزرسانِ state: آن تابع بعد از
    // پایان این هندلر اجرا می‌شود و تا آن لحظه ref روی کلید جدید تنظیم شده است.
    const anchorKey = lastToggledKeyRef.current
    const useRange = withRange && anchorKey !== null
    setSelectedKeys((prev) => {
      const next = new Set(prev)
      let keys = [key]
      if (useRange) {
        const from = pageItems.findIndex((d) => d.key === anchorKey)
        const to = pageItems.findIndex((d) => d.key === key)
        if (from !== -1 && to !== -1) {
          keys = pageItems.slice(Math.min(from, to), Math.max(from, to) + 1).map((d) => d.key)
        }
      }
      for (const k of keys) {
        if (checked) next.add(k)
        else next.delete(k)
      }
      return next
    })
    lastToggledKeyRef.current = key
  }

  // چک‌باکس سرستون فقط ردیف‌های همین صفحه را می‌گیرد یا رها می‌کند.
  function togglePage(checked: boolean) {
    setSelectedKeys((prev) => {
      const next = new Set(prev)
      for (const d of pageItems) {
        if (checked) next.add(d.key)
        else next.delete(d.key)
      }
      return next
    })
    lastToggledKeyRef.current = null
  }

  function clearSelection() {
    setSelectedKeys(new Set())
    lastToggledKeyRef.current = null
  }

  // دانلود گروهی: درخواست‌ها پشت‌سرهم فرستاده می‌شوند تا مرورگر آن‌ها را به‌عنوان
  // دانلودِ هم‌زمانِ انبوه بلاک نکند.
  async function handleBulkDownload() {
    if (bulk || selectedDocuments.length === 0) return
    const targets = selectedDocuments
    setBulk({ kind: 'download', done: 0, total: targets.length })
    const failed: string[] = []
    for (const [index, doc] of targets.entries()) {
      try {
        await downloadDocumentFile(doc.key, doc.fileName ?? doc.number)
      } catch {
        failed.push(toPersianDigits(doc.number))
      }
      setBulk({ kind: 'download', done: index + 1, total: targets.length })
    }
    setBulk(null)
    if (failed.length > 0) {
      window.alert('دانلود این اسناد ناموفق بود: ' + failed.join('، '))
    }
  }

  // آرشیوِ گروهی. اسنادِ از قبل آرشیوشده کنار گذاشته می‌شوند تا درخواست بی‌اثر
  // به سرور نرود و شمارشِ تأیید هم واقعی باشد.
  async function handleBulkDelete() {
    if (bulk) return
    const targets = selectedDocuments.filter((d) => d.isActive)
    if (targets.length === 0) {
      window.alert('هیچ سند فعالی در انتخاب شما نیست.')
      return
    }
    const confirmed = window.confirm(
      `${toPersianDigits(targets.length)} سند انتخاب‌شده آرشیو شوند؟ این اسناد دیگر در فهرست عادی اسناد نمایش داده نخواهند شد.`,
    )
    if (!confirmed) return

    setBulk({ kind: 'delete', done: 0, total: targets.length })
    const failed: string[] = []
    for (const [index, doc] of targets.entries()) {
      try {
        await documentApi.remove(doc.key)
      } catch {
        failed.push(toPersianDigits(doc.number))
      }
      setBulk({ kind: 'delete', done: index + 1, total: targets.length })
    }
    setBulk(null)
    clearSelection()
    loadDocuments()
    if (failed.length > 0) {
      window.alert('آرشیو کردن این اسناد ناموفق بود: ' + failed.join('، '))
    }
  }

  return (
    <div>
      <div className="page-header">
        <div className="page-header__text">
          <h1>اسناد</h1>
          <p>مرور و مدیریت اسناد کنترل شده</p>
        </div>
      </div>

      <div className="card">
        {/* کادر جستجوی این نوار حذف شد: جستجو حالا یک‌جا در نوار بالای برنامه است و
            داشتن دو کادر جستجو در یک صفحه فقط سردرگمی می‌ساخت. */}
        <div className="toolbar">
          <span className="toolbar__count">
            {isSearching ? 'نتیجه‌ی جستجو' : 'همه‌ی اسناد'}
            <strong>{toPersianDigits(totalCount)}</strong>
          </span>
          <div className="toolbar-actions">
            {/* کلیدِ «نمایش آرشیو» فقط برای مدیر اسناد - کاربرِ فقط-مشاهده اصلاً
                سند آرشیوشده‌ای نمی‌بیند که بخواهد نمایشش را روشن کند. */}
            {canManage && (
              <label className="inactive-toggle">
                <input
                  type="checkbox"
                  checked={includeInactive}
                  onChange={(e) => setIncludeInactive(e.target.checked)}
                />
                نمایش آرشیو
              </label>
            )}
            {/* خروجی اکسل فقط برای Admin و DocumentManager - شاملِ داده‌ی خامِ کلِ
                آرشیو است، نه همان نمایی که کاربرِ فقط‌مشاهده در فهرست می‌بیند. */}
            {canExport && (
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setIsExportDialogOpen(true)}
                disabled={isExporting || totalCount === 0}
              >
                خروجی اکسل
              </button>
            )}
            {canManage && (
              <Link to="/documents/reserve" className="btn btn-ghost">
                رزرو کد
              </Link>
            )}
            {canManage && (
              <Link to="/documents/new" className="btn btn-primary">
                افزودن سند
              </Link>
            )}
          </div>
        </div>

        {/* تراشه‌های فیلترِ فعال: چون فیلترها داخل یک پنل شناور تنظیم می‌شوند، بدون این
            نوار معلوم نبود نتیجه بر چه اساسی محدود شده. هر تراشه جداگانه پاک می‌شود. */}
        {isSearching && (
          <div className="filter-chips">
            {filters.query && (
              <button type="button" className="filter-chip" onClick={() => clearParam(SEARCH_PARAM.query)}>
                <span className="filter-chip__label">عبارت</span>
                <span className="filter-chip__value">{toPersianDigits(filters.query)}</span>
                <CloseIcon size={13} />
              </button>
            )}
            {filters.managementId && (
              <button type="button" className="filter-chip" onClick={() => clearParam(SEARCH_PARAM.management)}>
                <span className="filter-chip__label">مدیریت سازمانی</span>
                <CloseIcon size={13} />
              </button>
            )}
            {filters.activityId && (
              <button type="button" className="filter-chip" onClick={() => clearParam(SEARCH_PARAM.activity)}>
                <span className="filter-chip__label">فعالیت سازمانی</span>
                <CloseIcon size={13} />
              </button>
            )}
            {filters.onlyLatest && (
              <button type="button" className="filter-chip" onClick={() => clearParam(SEARCH_PARAM.onlyLatest)}>
                <span className="filter-chip__label">فقط آخرین بازنگری</span>
                <CloseIcon size={13} />
              </button>
            )}
            {filters.inFileContent && (
              <button type="button" className="filter-chip" onClick={() => clearParam(SEARCH_PARAM.inFileContent)}>
                <span className="filter-chip__label">جستجو در محتوای فایل</span>
                <CloseIcon size={13} />
              </button>
            )}
            <button type="button" className="filter-chips__clear" onClick={clearAllParams}>
              پاک کردن همه
            </button>
          </div>
        )}

        {/* نوارِ نما بالای خودِ محتوا می‌نشیند و هنگام بارگذاری هم می‌ماند: عوض کردن نما
            یک درخواست تازه می‌فرستد، و اگر کنترل در همان لحظه ناپدید می‌شد، هم جای
            محتوا می‌پرید و هم فوکوسِ کاربرِ کیبورد گم می‌شد. ردیف‌های قبلی تا رسیدن
            نتیجه‌ی تازه در حافظه‌اند، پس شرطش هم روی همان‌هاست نه روی isLoading. */}
        {!loadError && pageItems.length > 0 && (
          <div className="view-bar">
            <ViewSwitch
              label="نمای فهرست اسناد"
              value={viewMode}
              onChange={setViewMode}
              options={VIEW_OPTIONS}
            />
            {isTreeView && (
              <p className="view-bar__hint">گروه‌بندی بر پایه‌ی مدیریت سازمانی ← فعالیت سازمانی</p>
            )}
          </div>
        )}

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

        {!isLoading && !loadError && pageItems.length === 0 && (
          <EmptyState
            title="سندی یافت نشد."
            description={isSearching ? 'فیلترها را تغییر دهید یا عبارت دیگری را امتحان کنید.' : undefined}
          />
        )}

        {/* نمای درختی: کلِ نتیجه‌ی فیلترشده، گروه‌بندی‌شده بر پایه‌ی مدیریت ← فعالیت.
            صفحه‌بندی ندارد؛ فقط اگر نتیجه از سقف بگذرد همان سقف نشان داده می‌شود. */}
        {!isLoading && !loadError && pageItems.length > 0 && isTreeView && (
          <>
            {totalCount > pageItems.length && (
              <p className="doc-tree__note">
                نمایش {toPersianDigits(pageItems.length)} سند از {toPersianDigits(totalCount)} سند. برای
                دیدن بقیه، نتیجه را با جستجو یا فیلترها محدود کنید.
              </p>
            )}
            <DocumentTreeView
              documents={pageItems}
              labels={treeLabels}
              onOpen={handleView}
              onDownload={handleDownload}
              onHistory={(doc) => setHistoryDocument(doc)}
              onManage={openFormDrawer}
              onArchive={handleDelete}
              onRelations={(doc) => setRelationsDocument(doc)}
              onEditCoding={(doc) => navigate(`/documents/${doc.key}/coding`)}
              canManage={canManage}
              pendingFileId={pendingFileId}
              pendingDeleteId={pendingDeleteId}
            />
          </>
        )}

        {!isLoading && !loadError && pageItems.length > 0 && !isTreeView && (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    {/* ستون انتخاب چندتایی - سرستون، ردیف‌های همین صفحه را انتخاب می‌کند. */}
                    <th className="select-cell">
                      <label className="row-check">
                        <input
                          ref={selectAllRef}
                          type="checkbox"
                          checked={allPageSelected}
                          onChange={(e) => togglePage(e.target.checked)}
                          aria-label="انتخاب همه‌ی ردیف‌های این صفحه"
                        />
                      </label>
                    </th>
                    {/* شمارهٔ ردیف: پیوسته در کل نتیجه، نه فقط در صفحهٔ جاری. */}
                    <th className="row-index">ردیف</th>
                    <th>شماره</th>
                    <th>نام</th>
                    <th>بازنگری</th>
                    {/* ستون «شماره مدارک مرتبط» - شماره‌ی سمت مقابلِ هر ارتباط، از هر دو جهت. */}
                    <th>شماره مدارک مرتبط</th>
                    <th>تاریخ بازبینی</th>
                    <th>وضعیت</th>
                    {/* Always rendered: viewing a document's file is available to every
                        authenticated user, so this column is no longer manage-only. */}
                    <th className="actions-head">عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {pageItems.map((doc, index) => (
                    <tr key={doc.key} className={selectedKeys.has(doc.key) ? 'is-selected' : undefined}>
                      {/* چک‌باکس ردیف. انتخاب روی رویداد click انجام می‌شود نه change:
                          فقط click وضعیت کلید Shift را همراه دارد و انتخاب بازه‌ای به آن
                          نیاز دارد. کلیدِ Space صفحه‌کلید هم click تولید می‌کند، پس
                          دسترس‌پذیری از دست نمی‌رود. */}
                      <td className="select-cell">
                        <label className="row-check">
                          <input
                            type="checkbox"
                            checked={selectedKeys.has(doc.key)}
                            onClick={(e) => toggleRow(doc.key, e.currentTarget.checked, e.shiftKey)}
                            onChange={noop}
                            aria-label={`انتخاب سند ${toPersianDigits(doc.number)}`}
                          />
                        </label>
                      </td>
                      {/* شمارهٔ ردیف با احتساب صفحه‌بندی، پس صفحهٔ ۲ از ۱۱ شروع می‌شود. */}
                      <td className="row-index mono">{toPersianDigits((page - 1) * PAGE_SIZE + index + 1)}</td>
                      <td className="mono num-cell">
                        {toPersianDigits(doc.number)}
                        {/* نشانِ «خارج از کدینگ» زیر خودِ شماره می‌آید، چون مشکل دقیقاً
                            همان شماره است نه وضعیت سند. راهنمای شناور علتش را می‌گوید.
                            فقط برای کسی که می‌تواند سند مدیریت کند - کاربرِ فقط-مشاهده
                            نیازی به دیدنِ این جزئیاتِ داخلی ندارد. */}
                        {canManage && doc.isOutsideCodingStructure && (
                          <span
                            className="badge badge-warning coding-flag"
                            title="شماره‌ی این مدرک با ساختار کد فعلی نمی‌خواند؛ از سامانه‌ی قدیم با شماره‌ی خودش وارد شده است."
                          >
                            خارج از کدینگ
                          </span>
                        )}
                        {doc.relatedDocumentNumber && (
                          <span className="row-subtext">بازنگری از {toPersianDigits(doc.relatedDocumentNumber)}</span>
                        )}
                      </td>
                      <td>
                        {doc.name}
                        {/* وقتی تطابق در متن فایل رخ داده، همان تکه‌ی متن زیر نام سند
                            می‌آید - وگرنه ردیفی که نه شماره‌اش و نه نامش عبارت را ندارد،
                            بی‌دلیل در نتیجه به نظر می‌رسید. */}
                        {doc.contentSnippet && (
                          <span className="row-snippet" title={doc.contentSnippet}>
                            <SearchIcon size={12} />
                            {doc.contentSnippet}
                          </span>
                        )}
                      </td>
                      <td>
                        {documentVersionLabel(doc.lastVersion)}
                        {doc.contentRevision ? toPersianDigits(String(doc.contentRevision).padStart(2, '0')) : ''}
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
                                {toPersianDigits(number)}
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
                            {doc.isActive ? 'فعال' : 'آرشیو شده'}
                          </span>
                        )}
                      </td>
                      {/* عملیات ردیف: دکمه‌های متنی جای خود را به آیکون‌های شیشه‌ای دادند؛
                          نام هر عملیات روی راهنمای شناور و aria-label می‌ماند. */}
                      <td className="actions-cell">
                        <IconButton
                          label="مشاهده"
                          icon={<EyeIcon />}
                          tone="view"
                          onClick={() => handleView(doc)}
                          pending={pendingFileId === doc.key}
                          disabled={pendingFileId !== null}
                        />
                        <IconButton
                          label="دانلود"
                          icon={<DownloadIcon />}
                          tone="download"
                          onClick={() => handleDownload(doc)}
                          disabled={pendingFileId !== null}
                        />
                        {/* تاریخچه هم کارِ مدیریت است، نه صرفِ مشاهده - کاربرِ فقط‌مشاهده
                            باید فقط بتواند مدرک را ببیند و دانلود کند، همان قاعده‌ای که
                            نمای درختی هم رعایت می‌کند. علاوه بر آن، فقط وقتی معنا دارد
                            که زنجیره بیش از یک حلقه داشته باشد: یا این بازنگری چیزی را
                            جایگزین کرده، یا خودش جایگزین شده. */}
                        {canManage && (doc.relatedDocumentNumber || doc.isSuperseded) && (
                          <IconButton
                            label="تاریخچه"
                            icon={<HistoryIcon />}
                            tone="history"
                            onClick={() => setHistoryDocument(doc)}
                          />
                        )}
                        {/* دکمه‌ی «مدارک مرتبط» از این ستون برداشته شد: مدیریت ارتباط‌ها
                            داخل همین فرم انجام می‌شود و شماره‌های ستون مقابل هم برای
                            دیدنشان کلیک‌پذیر مانده‌اند. */}
                        {canManage && (
                          <>
                            {/* ویرایش و بازنگری یک دکمه‌اند: همیشه در دسترس است (مثل قبلاً
                                ویرایش)، و خودِ فرم بر پایه‌ی اینکه کاربر فایل پیوست می‌کند
                                یا نه، بین اصلاح اطلاعات و ثبت بازنگری واقعی تصمیم می‌گیرد -
                                روی نسخه‌ی منسوخ یا آرشیوشده فقط بخش فایل غیرفعال می‌شود. */}
                            <IconButton
                              label="بازنگری"
                              icon={<ReviseIcon />}
                              tone="revise"
                              onClick={() => openFormDrawer(doc)}
                            />
                            {/* عملیاتی استثنایی و جدا از «بازنگری»: شماره‌ی سند را مستقیم
                                عوض می‌کند، نه محتوایش را. صفحه‌ی جداگانه دارد چون طبقه‌بندیِ
                                سازنده‌ی شماره را جمع می‌کند - مثل ساختنِ سند. */}
                            <IconButton
                              label="ویرایش کدگذاری"
                              icon={<EditIcon />}
                              tone="edit"
                              onClick={() => navigate(`/documents/${doc.key}/coding`)}
                            />
                            {doc.isActive && (
                              <IconButton
                                label="آرشیو"
                                icon={<DeactivateIcon />}
                                tone="danger"
                                onClick={() => handleDelete(doc)}
                                pending={pendingDeleteId === doc.key}
                              />
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
              <span>{toPersianDigits(totalCount)} سند</span>
              <div className="pagination-controls">
                <button type="button" className="btn btn-secondary btn-sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                  قبلی
                </button>
                <span>
                  صفحه {toPersianDigits(page)} از {toPersianDigits(totalPages)}
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

      {/* داکِ شناور عملیات گروهی: به‌جای نواری که جدول را به پایین هل می‌داد، از لبه‌ی
          پایین بالا می‌آید. هم جای جدول را نمی‌گیرد، هم موقع اسکرول همیشه در دسترس است. */}
      {selectedDocuments.length > 0 && (
        <div className="bulk-dock" role="region" aria-label="عملیات گروهی روی اسناد انتخاب‌شده">
          <div className="bulk-dock__panel">
            <div className="bulk-dock__info">
              <span className="bulk-dock__count">{toPersianDigits(selectedDocuments.length)}</span>
              <span className="bulk-dock__label">
                {bulk
                  ? `${bulk.kind === 'download' ? 'در حال دانلود' : 'در حال آرشیو'} ${toPersianDigits(bulk.done)} از ${toPersianDigits(bulk.total)}`
                  : 'سند انتخاب شده'}
              </span>
            </div>

            <span className="bulk-dock__sep" aria-hidden="true" />

            <div className="bulk-dock__actions">
              <button
                type="button"
                className="bulk-action bulk-action--download"
                onClick={handleBulkDownload}
                disabled={bulk !== null}
              >
                {bulk?.kind === 'download' ? <SpinnerIcon size={15} /> : <DownloadIcon size={15} />}
                دانلود همه
              </button>
              {/* آرشیو فقط برای مدیر، و فقط وقتی سند فعالی در انتخاب باشد. */}
              {canManage && (
                <button
                  type="button"
                  className="bulk-action bulk-action--danger"
                  onClick={handleBulkDelete}
                  disabled={bulk !== null || selectedActiveCount === 0}
                >
                  {bulk?.kind === 'delete' ? <SpinnerIcon size={15} /> : <DeactivateIcon size={15} />}
                  آرشیو
                  {selectedActiveCount > 0 && selectedActiveCount !== selectedDocuments.length && (
                    <span className="bulk-action__badge">{toPersianDigits(selectedActiveCount)}</span>
                  )}
                </button>
              )}
            </div>

            <button
              type="button"
              className="bulk-dock__clear"
              onClick={clearSelection}
              disabled={bulk !== null}
              aria-label="لغو انتخاب"
            >
              <CloseIcon size={15} />
            </button>

            {/* نوار پیشرفت باریک، چسبیده به لبه‌ی پایینِ داک. */}
            {bulk && (
              <span
                className="bulk-dock__progress"
                style={{ transform: `scaleX(${bulk.done / bulk.total})` }}
                aria-hidden="true"
              />
            )}
          </div>
        </div>
      )}

      {drawer && (
        <DocumentFormDrawer
          // Remounts when the target changes, so the form's initial state is
          // re-derived from the new document instead of being kept from the old one.
          key={drawer.key}
          document={drawer}
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

      {previewDocument && (
        <DocumentPreviewModal document={previewDocument} onClose={() => setPreviewDocument(null)} />
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

      {isExportDialogOpen && (
        <ExportColumnsDialog
          onClose={() => setIsExportDialogOpen(false)}
          onConfirm={handleExport}
          isExporting={isExporting}
        />
      )}
    </div>
  )
}