import { useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
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
  EditIcon,
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
  onEditCoding: (d: DocumentDto) => void
  // فقط مشاهده و دانلود همیشه در دسترس‌اند؛ تاریخچه، بازنگری و آرشیو کارِ مدیریتِ
  // سند است، پس پشتِ همان قابلیتی می‌مانند که در نمای جدولی هم این عملیات را
  // نشان می‌دهد - کاربرِ فقط‌مشاهده در هیچ‌کدام از دو نما نباید بیشتر از این ببیند.
  canManage: boolean
  pendingFileId: string | null
  pendingDeleteId: string | null
}

// عمقِ درخت فقط داخلِ سلولِ «نام» به‌صورت تورفتگی نمایش داده می‌شود (متغیرِ ‎--depth‎).
// هیچ ستونِ دیگری با عمق جابه‌جا نمی‌شود، چون همه‌ی ردیف‌ها - سرستون، پوشه‌ها و اسناد -
// از یک تعریفِ ستونیِ واحد (‎--doc-tree-cols‎ در CSS) استفاده می‌کنند.
const INDENT_STYLE = (depth: number): CSSProperties => ({ ['--depth' as string]: depth } as CSSProperties)

// ---- ردیفِ پوشه (مدیریت/فعالیت): دکمه‌ی باز/بسته‌کردن ----
// برخلافِ ردیف‌های سند که گریدِ ۸ستونی‌اند، ردیفِ پوشه یک سطرِ تمام‌عرض است: عنوان
// (شِوران، آیکون، کد، برچسب) کاملاً سمتِ راست می‌نشیند و شمارنده تا آخرِ سطر به چپ
// می‌رود. تورفتگیِ درخت هم با --depth روی همین سطر اعمال می‌شود، پس فعالیت کمی
// تورفته‌تر از مدیریت دیده می‌شود. کلِ سطر یک <button> است (aria-expanded + صفحه‌کلید).
function FolderRow({
  branch,
  depth,
  open,
  onToggle,
}: {
  branch: Branch
  depth: number
  open: boolean
  onToggle: () => void
}) {
  const FolderIcon = depth === 0 ? BuildingIcon : LayersIcon
  return (
    <button
      type="button"
      className="doc-tree__row doc-tree__row--folder"
      data-depth={depth}
      aria-expanded={open}
      onClick={onToggle}
      style={INDENT_STYLE(depth)}
    >
      <span className="doc-tree__chevron" aria-hidden="true">
        <ChevronDownIcon size={14} />
      </span>
      <span className="doc-tree__folder" aria-hidden="true">
        <FolderIcon size={15} />
      </span>
      {branch.code && <span className="doc-tree__code mono">{toPersianDigits(branch.code)}</span>}
      <span className="doc-tree__label">{branch.title}</span>
      <span className="doc-tree__count">{toPersianDigits(branch.count)}</span>
    </button>
  )
}

// ---- ردیفِ سند (برگ): همان هشت ستونِ نمای جدولی ----
function LeafRow({ document, rowIndex, depth, actions }: { document: DocumentDto; rowIndex: number; depth: number; actions: TreeActions }) {
  const {
    onOpen,
    onDownload,
    onHistory,
    onManage,
    onArchive,
    onRelations,
    onEditCoding,
    canManage,
    pendingFileId,
    pendingDeleteId,
  } = actions

  return (
    <div className="doc-tree__row doc-tree__row--leaf">
      <span className="doc-tree__c-index mono">{toPersianDigits(rowIndex)}</span>

      <span className="doc-tree__c-number">
        {/* mono فقط روی خودِ شماره است، نه کلِ سلول: اگر روی سلول باشد، ‎direction:ltr‎ی
            که mono می‌گذارد محورِ چینشِ سلول را برعکس می‌کند و مقدار به‌جای راست، چپ
            می‌چسبد و زیرِ سرستونِ راست‌چینِ «شماره» نمی‌نشیند. */}
        <span className="doc-tree__number-value mono">{toPersianDigits(document.number)}</span>
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

      <span className="doc-tree__c-name doc-tree__c-name--leaf" style={INDENT_STYLE(depth)}>
        <span className="doc-tree__name-text" title={document.name}>
          {document.name}
        </span>
        {document.contentSnippet && (
          <span className="row-snippet" title={document.contentSnippet}>
            <SearchIcon size={12} />
            {document.contentSnippet}
          </span>
        )}
      </span>

      <span className="doc-tree__c-revision">
        {documentVersionLabel(document.lastVersion)}
        {document.contentRevision ? toPersianDigits(String(document.contentRevision).padStart(2, '0')) : ''}
      </span>

      <span className="doc-tree__c-related">
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

      <span className="doc-tree__c-review">{isoToJalaliText(document.currentReviewDate ?? '') || '—'}</span>

      <span className="doc-tree__c-status">
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

      <span className="doc-tree__c-actions">
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
            <IconButton
              label="ویرایش کدگذاری"
              icon={<EditIcon />}
              tone="edit"
              onClick={() => onEditCoding(document)}
            />
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
    </div>
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
  onEditCoding,
  canManage,
  pendingFileId,
  pendingDeleteId,
}: {
  documents: DocumentDto[]
  labels: DocumentTreeLabels
} & TreeActions) {
  const tree = useMemo(() => buildTree(documents, labels), [documents, labels])
  // شناسه‌ی شاخه‌های بازشده. پیش‌فرض همه بسته - مثل رفتار قبلیِ <details> - چون با
  // ۷۰۰+ سند، بازبودنِ پیش‌فرض یک صفحه‌ی شلوغ می‌داد.
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const actions: TreeActions = {
    onOpen,
    onDownload,
    onHistory,
    onManage,
    onArchive,
    onRelations,
    onEditCoding,
    canManage,
    pendingFileId,
    pendingDeleteId,
  }

  // رندرِ بازگشتی به‌صورت تخت: هر شاخه یک ردیفِ پوشه، و در صورت باز بودن، زیرشاخه‌ها و
  // اسنادش. هیچ لایه‌ای تورفتگیِ افقی به ردیف‌ها اضافه نمی‌کند؛ تورفتگیِ درخت فقط از
  // ‎--depth‎ داخلِ سلولِ نام می‌آید، پس همه‌ی ردیف‌ها هم‌عرضِ سرستون می‌مانند.
  function renderBranch(branch: Branch, depth: number) {
    const open = expanded.has(branch.id)
    return (
      <div className="doc-tree__group" key={branch.id}>
        <FolderRow branch={branch} depth={depth} open={open} onToggle={() => toggle(branch.id)} />
        {open && (
          <>
            {branch.children.map((child) => renderBranch(child, depth + 1))}
            {branch.leaves.map((leaf) => (
              <LeafRow
                key={leaf.document.key}
                document={leaf.document}
                rowIndex={leaf.rowIndex}
                depth={depth + 1}
                actions={actions}
              />
            ))}
          </>
        )}
      </div>
    )
  }

  return (
    <div className="doc-tree">
      <div className="doc-tree__inner">
        {/* سرستونِ واحد. از همان ‎--doc-tree-cols‎ی استفاده می‌کند که ردیف‌ها؛ پس
            مرزهای ستونی از سرستون تا آخرین ردیف یکی است و با ردیف‌ها اسکرول افقی می‌خورد. */}
        <div className="doc-tree__row doc-tree__row--head">
          <span className="doc-tree__c-index">ردیف</span>
          <span className="doc-tree__c-number">شماره</span>
          <span className="doc-tree__c-name">نام</span>
          <span className="doc-tree__c-revision">بازنگری</span>
          <span className="doc-tree__c-related">مدارک مرتبط</span>
          <span className="doc-tree__c-review">تاریخ بازبینی</span>
          <span className="doc-tree__c-status">وضعیت</span>
          <span className="doc-tree__c-actions">عملیات</span>
        </div>

        {tree.map((branch) => renderBranch(branch, 0))}
      </div>
    </div>
  )
}
