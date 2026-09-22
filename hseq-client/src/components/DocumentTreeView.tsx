import { useMemo } from 'react'
import type { DocumentDto } from '../types/api'
import { documentVersionLabel } from '../types/api'
import { toPersianDigits } from '../lib/digits'
import { isoToJalaliText } from '../lib/jalali'
import { IconButton } from './IconButton'
import {
  BuildingIcon,
  ChevronDownIcon,
  DeactivateIcon,
  DownloadIcon,
  EyeIcon,
  HistoryIcon,
  LayersIcon,
  ReviseIcon,
  SearchIcon,
} from './Icons'

// نگاشتِ شناسه به کد/عنوان، برای برچسبِ هر شاخه. از لوکاپ‌های اطلاعات پایه ساخته
// می‌شود؛ کد هم می‌آید چون کاربر همان کد را روی شماره‌ی هر مدرک می‌بیند.
export interface DocumentTreeGroupLabel {
  code: string
  title: string
}
export interface DocumentTreeLabels {
  managements: Map<string, DocumentTreeGroupLabel>
  activities: Map<string, DocumentTreeGroupLabel>
}

// شاخه‌ای که هیچ عنوانی در اطلاعات پایه برایش پیدا نشد. با داده‌ی درست هرگز ساخته
// نمی‌شود، اما اگر ساخته شد سند را قربانی نمی‌کند - فقط ته فهرست می‌نشیند.
const UNKNOWN_MANAGEMENT = 'سایر مدیریت‌ها'
const UNKNOWN_ACTIVITY = 'عمومی / سایر فعالیت‌ها'

// ردیف با همان شماره‌ای شمرده می‌شود که اگر کل درخت باز بود از بالا می‌دیدید - یعنی
// دقیقاً همان مفهومِ «ردیف» در نمای جدولی، نه یک شمارنده‌ی جداگانه برای هر گروه.
interface Leaf {
  document: DocumentDto
  rowIndex: number
}
interface Branch {
  id: string
  code: string
  title: string
  children: Branch[]
  leaves: Leaf[]
  count: number
}

// ترتیبِ شاخه‌ها همان ترتیبِ کدینگ سازمانی است (A، C، D…) نه الفبای عنوان: کد روی
// شماره‌ی هر مدرک دیده می‌شود، پس پیدا کردنِ شاخه با کد سریع‌تر از خواندنِ عنوان‌هاست.
const collator = new Intl.Collator('fa', { numeric: true, sensitivity: 'base' })

function compareBranches(a: { code: string; title: string }, b: { code: string; title: string }): number {
  if (a.code && b.code) return collator.compare(a.code, b.code) || collator.compare(a.title, b.title)
  // شاخه‌ی بی‌کد یعنی شناسه‌ای که در اطلاعات پایه نبود؛ همیشه آخر.
  if (a.code) return -1
  if (b.code) return 1
  return collator.compare(a.title, b.title)
}

function groupBy(documents: DocumentDto[], keyOf: (d: DocumentDto) => string): [string, DocumentDto[]][] {
  const buckets = new Map<string, DocumentDto[]>()
  for (const document of documents) {
    const key = keyOf(document)
    const bucket = buckets.get(key)
    if (bucket) bucket.push(document)
    else buckets.set(key, [document])
  }
  return [...buckets.entries()]
}

function buildTree(documents: DocumentDto[], labels: DocumentTreeLabels): Branch[] {
  // سطح ۱: مدیریت سازمانی ← سطح ۲: فعالیت سازمانی ← اسناد.
  //
  // هر سطح دقیقاً از همان فیلدِ خودِ سند گروه می‌شود (organizationalManagementId و
  // organizationalActivityId)، نه از رابطه‌ی حدس‌زده‌ی بین لوکاپ‌ها؛ یعنی آنچه در درخت
  // دیده می‌شود همان چیزی است که در فرمِ سند و در جدول ثبت شده. برچسب و ترتیب از
  // اطلاعات پایه می‌آید.
  //
  // شمارنده‌ی ردیف بعد از مرتب‌سازیِ شاخه‌ها پر می‌شود (پایین‌تر)، نه همین‌جا؛ ترتیبِ
  // واقعیِ نمایش فقط پس از sort کردنِ مدیریت‌ها و فعالیت‌ها معلوم می‌شود.
  const managements = groupBy(documents, (d) => d.organizationalManagementId)
    .map(([managementId, managementDocuments]) => {
      const management = labels.managements.get(managementId)
      const activities = groupBy(managementDocuments, (d) => d.organizationalActivityId)
        .map(([activityId, activityDocuments]) => {
          const activity = labels.activities.get(activityId)
          return {
            id: `${managementId}/${activityId}`,
            code: activity?.code ?? '',
            title: activity?.title ?? UNKNOWN_ACTIVITY,
            documents: activityDocuments.slice().sort((a, b) => collator.compare(a.number, b.number)),
          }
        })
        .sort(compareBranches)

      return {
        id: managementId,
        code: management?.code ?? '',
        title: management?.title ?? UNKNOWN_MANAGEMENT,
        activities,
      }
    })
    .sort(compareBranches)

  let rowIndex = 0
  return managements.map((m) => {
    const activityBranches = m.activities.map<Branch>((a) => ({
      id: a.id,
      code: a.code,
      title: a.title,
      children: [],
      leaves: a.documents.map((document) => ({ document, rowIndex: ++rowIndex })),
      count: a.documents.length,
    }))
    const managementCount = activityBranches.reduce((sum, a) => sum + a.count, 0)
    return { id: m.id, code: m.code, title: m.title, children: activityBranches, leaves: [], count: managementCount }
  })
}

interface TreeActions {
  onOpen: (d: DocumentDto) => void
  onDownload: (d: DocumentDto) => void
  onHistory: (d: DocumentDto) => void
  onManage: (d: DocumentDto) => void
  onArchive: (d: DocumentDto) => void
  onRelations: (d: DocumentDto) => void
  // فقط مشاهده و دانلود همیشه در دسترس‌اند؛ تاریخچه، بازنگری و آرشیو کارِ مدیریتِ
  // سند است، پس پشتِ همان قابلیتی می‌مانند که در نمای جدولی هم این عملیات را
  // نشان می‌دهد - کاربرِ فقط‌مشاهده در هیچ‌کدام از دو نما نباید بیشتر از این ببیند.
  canManage: boolean
  pendingFileId: string | null
  pendingDeleteId: string | null
}

function TreeBranch({ branch, level, actions }: { branch: Branch; level: number; actions: TreeActions }) {
  // <details> برای بازوبسته‌شدنِ نیتیو - بدون state، دسترس‌پذیر و سبک. هر دو سطح با
  // ورود به نمای درختی بسته‌اند؛ کاربر خودش هر شاخه‌ای را که می‌خواهد باز می‌کند،
  // چون با ۷۰۰+ سند بازبودنِ پیش‌فرضِ سطح مدیریت یک صفحه‌ی پر از سند نامرتب می‌داد.
  // آیکونِ فولدرِ سطح: مدیریت=ساختمان، فعالیت=لایه‌ها.
  const FolderIcon = level === 0 ? BuildingIcon : LayersIcon
  const { onOpen, onDownload, onHistory, onManage, onArchive, onRelations, canManage, pendingFileId, pendingDeleteId } =
    actions

  return (
    <details className={`doc-tree__branch doc-tree__branch--l${level}`}>
      <summary className="doc-tree__summary">
        {/* شِوران در RTL: جمع = رو به چپ، باز = رو به پایین (چرخش با CSS). */}
        <span className="doc-tree__chevron" aria-hidden="true">
          <ChevronDownIcon size={14} />
        </span>
        <span className="doc-tree__folder" aria-hidden="true">
          <FolderIcon size={15} />
        </span>
        {branch.code && <span className="doc-tree__code mono">{toPersianDigits(branch.code)}</span>}
        <span className="doc-tree__label">{branch.title}</span>
        <span className="doc-tree__count">{toPersianDigits(branch.count)}</span>
      </summary>

      {branch.children.map((child) => (
        <TreeBranch key={child.id} branch={child} level={level + 1} actions={actions} />
      ))}

      {branch.leaves.length > 0 && (
        <div className="doc-tree__leaves-scroll">
          {/* همان ستون‌های نمای جدولی، همین‌جا هم - فقط چیده‌شده زیرِ فعالیتِ خودشان
              به‌جای یک جدولِ تخت. */}
          <div className="doc-tree__leaves-head">
            <span>ردیف</span>
            <span>شماره</span>
            <span>نام</span>
            <span>بازنگری</span>
            <span>مدارک مرتبط</span>
            <span>تاریخ بازبینی</span>
            <span>وضعیت</span>
            <span>عملیات</span>
          </div>
          <ul className="doc-tree__leaves">
            {branch.leaves.map(({ document, rowIndex }) => (
              <li key={document.key} className="doc-tree__leaf-row">
                <span className="doc-tree__leaf-index mono">{toPersianDigits(rowIndex)}</span>

                <span className="doc-tree__leaf-number mono">
                  {toPersianDigits(document.number)}
                  {canManage && document.isOutsideCodingStructure && (
                    <span
                      className="badge badge-warning coding-flag"
                      title="شماره‌ی این مدرک با ساختار کد فعلی نمی‌خواند؛ از سامانه‌ی قدیم با شماره‌ی خودش وارد شده است."
                    >
                      خارج از کدینگ
                    </span>
                  )}
                  {document.relatedDocumentNumber && (
                    <span className="row-subtext">بازنگری از {toPersianDigits(document.relatedDocumentNumber)}</span>
                  )}
                </span>

                <span className="doc-tree__leaf-name" title={document.name}>
                  {document.name}
                  {document.contentSnippet && (
                    <span className="row-snippet" title={document.contentSnippet}>
                      <SearchIcon size={12} />
                      {document.contentSnippet}
                    </span>
                  )}
                </span>

                <span className="doc-tree__leaf-revision">
                  {documentVersionLabel(document.lastVersion)}
                  {document.contentRevision ? toPersianDigits(String(document.contentRevision).padStart(2, '0')) : ''}
                </span>

                <span className="doc-tree__leaf-related">
                  {document.relatedDocumentNumbers.length === 0 ? (
                    '—'
                  ) : (
                    <button
                      type="button"
                      className="related-numbers"
                      onClick={() => onRelations(document)}
                      title="مشاهده و مدیریت مدارک مرتبط"
                    >
                      {document.relatedDocumentNumbers.map((number) => (
                        <span key={number} className="badge badge-muted mono">
                          {toPersianDigits(number)}
                        </span>
                      ))}
                    </button>
                  )}
                </span>

                <span className="doc-tree__leaf-review">{isoToJalaliText(document.currentReviewDate ?? '') || '—'}</span>

                <span className="doc-tree__leaf-status">
                  {/* یک نسخه‌ی منسوخ هم غیرفعال است، پس این شرط باید اول چک شود -
                      وگرنه به نظر «حذف‌شده» می‌رسید. */}
                  {document.isSuperseded ? (
                    <span className="badge badge-muted">منسوخ (بازنگری شده)</span>
                  ) : (
                    <span className={`badge ${document.isActive ? 'badge-success' : 'badge-danger'}`}>
                      {document.isActive ? 'فعال' : 'آرشیو شده'}
                    </span>
                  )}
                </span>

                <span className="doc-tree__leaf-actions">
                  <IconButton
                    label="مشاهده"
                    icon={<EyeIcon />}
                    tone="view"
                    onClick={() => onOpen(document)}
                    pending={pendingFileId === document.key}
                    disabled={pendingFileId !== null}
                  />
                  <IconButton
                    label="دانلود"
                    icon={<DownloadIcon />}
                    tone="download"
                    onClick={() => onDownload(document)}
                    disabled={pendingFileId !== null}
                  />
                  {canManage && (document.relatedDocumentNumber || document.isSuperseded) && (
                    <IconButton label="تاریخچه" icon={<HistoryIcon />} tone="history" onClick={() => onHistory(document)} />
                  )}
                  {canManage && (
                    <>
                      <IconButton label="بازنگری" icon={<ReviseIcon />} tone="revise" onClick={() => onManage(document)} />
                      {document.isActive && (
                        <IconButton
                          label="آرشیو"
                          icon={<DeactivateIcon />}
                          tone="danger"
                          onClick={() => onArchive(document)}
                          pending={pendingDeleteId === document.key}
                        />
                      )}
                    </>
                  )}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </details>
  )
}

export function DocumentTreeView({
  documents,
  labels,
  onOpen,
  onDownload,
  onHistory,
  onManage,
  onArchive,
  onRelations,
  canManage,
  pendingFileId,
  pendingDeleteId,
}: {
  documents: DocumentDto[]
  labels: DocumentTreeLabels
} & TreeActions) {
  const tree = useMemo(() => buildTree(documents, labels), [documents, labels])
  const actions: TreeActions = {
    onOpen,
    onDownload,
    onHistory,
    onManage,
    onArchive,
    onRelations,
    canManage,
    pendingFileId,
    pendingDeleteId,
  }

  return (
    <div className="doc-tree">
      {tree.map((branch) => (
        <TreeBranch key={branch.id} branch={branch} level={0} actions={actions} />
      ))}
    </div>
  )
}
