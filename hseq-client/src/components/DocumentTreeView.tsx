import { useMemo } from 'react'
import type { DocumentDto } from '../types/api'
import { toPersianDigits } from '../lib/digits'
import { BuildingIcon, ChevronDownIcon, EyeIcon, FileTextIcon, LayersIcon } from './Icons'

// نگاشتِ شناسه به عنوان، برای برچسبِ هر شاخه. از لوکاپ‌های اطلاعات پایه ساخته می‌شود.
// activityParent: فعالیت ← مدیریتِ والدش، تا مدیریتِ هر سند از سلسله‌مراتبِ واقعی
// (فعالیت→مدیریت) گرفته شود، نه فقط از فیلدِ managementId خودِ سند.
export interface DocumentTreeLabels {
  managements: Map<string, string>
  activities: Map<string, string>
  activityParent: Map<string, string>
}

interface Leaf {
  document: DocumentDto
}
interface Branch {
  id: string
  label: string
  children: Branch[]
  leaves: Leaf[]
  count: number
}

// اسناد بر اساس سلسله‌مراتبِ نوع سند ← مدیریت سازمانی ← فعالیت سازمانی گروه می‌شوند،
// و برگ‌ها خودِ اسناد (شماره/عنوان). گروه‌های بی‌عنوان (شناسه‌ی ناشناخته) با «—»
// نمایش داده می‌شوند تا سندی گم نشود.
function groupBy(
  documents: DocumentDto[],
  keyOf: (d: DocumentDto) => string,
  labelOf: (key: string) => string,
): { key: string; label: string; items: DocumentDto[] }[] {
  const buckets = new Map<string, DocumentDto[]>()
  for (const document of documents) {
    const key = keyOf(document)
    const bucket = buckets.get(key)
    if (bucket) bucket.push(document)
    else buckets.set(key, [document])
  }
  return [...buckets.entries()]
    .map(([key, items]) => ({ key, label: labelOf(key), items }))
    .sort((a, b) => a.label.localeCompare(b.label, 'fa'))
}

function buildTree(documents: DocumentDto[], labels: DocumentTreeLabels): Branch[] {
  // سطح ۱: مدیریت سازمانی ← سطح ۲: فعالیت سازمانی ← اسناد.
  // مدیریتِ سند از والدِ فعالیتش گرفته می‌شود؛ اگر فعالیت در لوکاپ نبود، از managementId
  // خودِ سند. نبودِ عنوان به‌جای «—» با برچسبِ گویا پر می‌شود تا سندی گم‌شده به نظر نرسد.
  const mgmtIdOf = (d: DocumentDto) =>
    labels.activityParent.get(d.organizationalActivityId) ?? d.organizationalManagementId
  const mgmtLabel = (id: string) => labels.managements.get(id) ?? 'سایر مدیریت‌ها'
  const actLabel = (id: string) => labels.activities.get(id) ?? 'عمومی / سایر فعالیت‌ها'

  return groupBy(documents, mgmtIdOf, mgmtLabel).map(
    (mgmtGroup) => {
      const activities = groupBy(
        mgmtGroup.items,
        (d) => d.organizationalActivityId,
        actLabel,
      ).map<Branch>((actGroup) => ({
        id: `${mgmtGroup.key}/${actGroup.key}`,
        label: actGroup.label,
        children: [],
        leaves: actGroup.items
          .slice()
          .sort((a, b) => a.number.localeCompare(b.number, 'fa'))
          .map((document) => ({ document })),
        count: actGroup.items.length,
      }))
      return { id: mgmtGroup.key, label: mgmtGroup.label, children: activities, leaves: [], count: mgmtGroup.items.length }
    },
  )
}

function TreeBranch({ branch, level, onOpen }: { branch: Branch; level: number; onOpen: (d: DocumentDto) => void }) {
  // <details> برای بازوبسته‌شدنِ نیتیو - بدون state، دسترس‌پذیر و سبک. سطح اول (مدیریت)
  // باز است، سطح دوم (فعالیت) جمع. آیکونِ فولدرِ سطح: مدیریت=ساختمان، فعالیت=لایه‌ها.
  const FolderIcon = level === 0 ? BuildingIcon : LayersIcon
  return (
    <details className={`doc-tree__branch doc-tree__branch--l${level}`} open={level === 0}>
      <summary className="doc-tree__summary">
        {/* شِوران در RTL: جمع = رو به چپ، باز = رو به پایین (چرخش با CSS). */}
        <span className="doc-tree__chevron" aria-hidden="true">
          <ChevronDownIcon size={14} />
        </span>
        <span className="doc-tree__folder" aria-hidden="true">
          <FolderIcon size={15} />
        </span>
        <span className="doc-tree__label">{branch.label}</span>
        <span className="doc-tree__count">{toPersianDigits(branch.count)}</span>
      </summary>

      {branch.children.map((child) => (
        <TreeBranch key={child.id} branch={child} level={level + 1} onOpen={onOpen} />
      ))}

      {branch.leaves.length > 0 && (
        <ul className="doc-tree__leaves">
          {branch.leaves.map(({ document }) => (
            <li key={document.key} className="doc-tree__leaf-row">
              <FileTextIcon size={14} />
              <span className="doc-tree__leaf-code mono">{toPersianDigits(document.number)}</span>
              <span className="doc-tree__leaf-name" title={document.name}>
                {document.name}
              </span>
              <button
                type="button"
                className="doc-tree__leaf-action"
                onClick={() => onOpen(document)}
                aria-label={`مشاهده ${document.number}`}
                title="مشاهده"
              >
                <EyeIcon size={15} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </details>
  )
}

export function DocumentTreeView({
  documents,
  labels,
  onOpen,
}: {
  documents: DocumentDto[]
  labels: DocumentTreeLabels
  onOpen: (document: DocumentDto) => void
}) {
  const tree = useMemo(() => buildTree(documents, labels), [documents, labels])

  return (
    <div className="doc-tree">
      {tree.map((branch) => (
        <TreeBranch key={branch.id} branch={branch} level={0} onOpen={onOpen} />
      ))}
    </div>
  )
}
