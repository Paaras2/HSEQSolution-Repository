import { useMemo } from 'react'
import type { DocumentDto } from '../types/api'
import { toPersianDigits } from '../lib/digits'
import { FileTextIcon } from './Icons'

// نگاشتِ شناسه به عنوان، برای برچسبِ هر شاخه. از لوکاپ‌های اطلاعات پایه ساخته می‌شود.
export interface DocumentTreeLabels {
  types: Map<string, string>
  managements: Map<string, string>
  activities: Map<string, string>
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
  const label = (map: Map<string, string>, id: string) => map.get(id) ?? '—'

  return groupBy(documents, (d) => d.documentTypeId, (id) => label(labels.types, id)).map((typeGroup) => {
    const managements = groupBy(
      typeGroup.items,
      (d) => d.organizationalManagementId,
      (id) => label(labels.managements, id),
    ).map((mgmtGroup) => {
      const activities = groupBy(
        mgmtGroup.items,
        (d) => d.organizationalActivityId,
        (id) => label(labels.activities, id),
      ).map<Branch>((actGroup) => ({
        id: `${typeGroup.key}/${mgmtGroup.key}/${actGroup.key}`,
        label: actGroup.label,
        children: [],
        leaves: actGroup.items
          .slice()
          .sort((a, b) => a.number.localeCompare(b.number, 'fa'))
          .map((document) => ({ document })),
        count: actGroup.items.length,
      }))
      return { id: `${typeGroup.key}/${mgmtGroup.key}`, label: mgmtGroup.label, children: activities, leaves: [], count: mgmtGroup.items.length }
    })
    return { id: typeGroup.key, label: typeGroup.label, children: managements, leaves: [], count: typeGroup.items.length }
  })
}

function TreeBranch({ branch, level, onOpen }: { branch: Branch; level: number; onOpen: (d: DocumentDto) => void }) {
  // <details> برای بازوبسته‌شدنِ نیتیو - بدون state، دسترس‌پذیر و سبک. سطح اول باز است.
  return (
    <details className="doc-tree__branch" open={level === 0} style={{ marginInlineStart: level === 0 ? 0 : 14 }}>
      <summary className="doc-tree__summary">
        <span className="doc-tree__label">{branch.label}</span>
        <span className="doc-tree__count">{toPersianDigits(branch.count)}</span>
      </summary>

      {branch.children.map((child) => (
        <TreeBranch key={child.id} branch={child} level={level + 1} onOpen={onOpen} />
      ))}

      {branch.leaves.length > 0 && (
        <ul className="doc-tree__leaves">
          {branch.leaves.map(({ document }) => (
            <li key={document.key}>
              <button type="button" className="doc-tree__leaf" onClick={() => onOpen(document)} title={document.name}>
                <FileTextIcon size={14} />
                <span className="doc-tree__leaf-code mono">{toPersianDigits(document.number)}</span>
                <span className="doc-tree__leaf-name">{document.name}</span>
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
