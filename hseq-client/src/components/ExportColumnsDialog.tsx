import { useEffect, useState } from 'react'
import { Modal } from './Modal'
import { searchApi } from '../api/searchApi'
import { ApiError } from '../lib/httpClient'
import type { ExportColumn } from '../types/api'
import { LoadingState, ErrorState } from './StateViews'

// آخرین انتخابِ کاربر برای دفعه‌ی بعد، فقط در همین مرورگر - یک راحتیِ محلی، نه
// تنظیمی که باید بین دستگاه‌ها همگام بماند. اگر خواندن/نوشتن‌اش شکست بخورد (حالت
// خصوصی، دیتای سایت مسدود)، بی‌صدا نادیده گرفته می‌شود و فرم روی پیش‌فرضِ سرور می‌ایستد.
const STORAGE_KEY = 'hseq.documents.exportColumns'

function loadSavedColumnKeys(): string[] | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    return Array.isArray(parsed) && parsed.every((v) => typeof v === 'string') ? (parsed as string[]) : null
  } catch {
    return null
  }
}

function saveColumnKeys(keys: string[]) {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(keys))
  } catch {
    // localStorage در دسترس نیست - انتخاب فقط برای همین بار اثر می‌کند.
  }
}

interface ExportColumnsDialogProps {
  onClose: () => void
  onConfirm: (columnKeys: string[]) => void
  isExporting: boolean
}

// دیالوگِ سفارشی‌سازیِ خروجی اکسل - فقط از داخلِ دکمه‌ی «خروجی اکسل» باز می‌شود، که
// خودش پشتِ قابلیتِ documents:export (Admin و DocumentManager) است؛ این‌جا دیگر
// نیازی به چک دوباره‌ی نقش نیست.
export function ExportColumnsDialog({ onClose, onConfirm, isExporting }: ExportColumnsDialogProps) {
  const [columns, setColumns] = useState<ExportColumn[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<Set<string>>(new Set())

  useEffect(() => {
    let cancelled = false
    searchApi
      .getExportColumns()
      .then((all) => {
        if (cancelled) return
        setColumns(all)
        const saved = loadSavedColumnKeys()
        const validSaved = saved?.filter((key) => all.some((c) => c.key === key))
        const initial = validSaved && validSaved.length > 0 ? validSaved : all.filter((c) => c.isDefault).map((c) => c.key)
        setSelected(new Set(initial))
      })
      .catch((err) => {
        if (!cancelled) setLoadError(err instanceof ApiError ? err.message : 'امکان بارگذاری فهرست ستون‌ها وجود ندارد.')
      })
    return () => {
      cancelled = true
    }
  }, [])

  function toggle(key: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function resetToDefault() {
    if (columns) setSelected(new Set(columns.filter((c) => c.isDefault).map((c) => c.key)))
  }

  function handleConfirm() {
    if (!columns || selected.size === 0) return
    // ترتیبِ ستون‌ها در فایل همان ترتیبِ فهرستِ سرور بماند، نه ترتیبِ کلیک‌کردنِ کاربر.
    const ordered = columns.filter((c) => selected.has(c.key)).map((c) => c.key)
    saveColumnKeys(ordered)
    onConfirm(ordered)
  }

  return (
    <Modal
      title="ستون‌های خروجی اکسل"
      subtitle="ستون‌هایی که می‌خواهید در فایل باشند را انتخاب کنید؛ انتخابتان برای دفعه‌ی بعد هم نگه داشته می‌شود."
      size="lg"
      onClose={onClose}
    >
      <div className="modal-body">
        {!columns && !loadError && <LoadingState title="در حال بارگذاری فهرست ستون‌ها..." />}

        {loadError && (
          <ErrorState
            title={loadError}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={onClose}>
                بستن
              </button>
            }
          />
        )}

        {columns && (
          <>
            <div className="export-columns-toolbar">
              <button type="button" className="btn btn-ghost btn-sm" onClick={resetToDefault}>
                بازگشت به پیش‌فرض
              </button>
            </div>

            <ul className="export-columns-grid">
              {columns.map((c) => (
                <li key={c.key}>
                  <label className="export-columns-grid__item">
                    <input type="checkbox" checked={selected.has(c.key)} onChange={() => toggle(c.key)} />
                    {c.label}
                  </label>
                </li>
              ))}
            </ul>

            {selected.size === 0 && (
              <p className="form-error" role="alert">
                حداقل یک ستون را انتخاب کنید.
              </p>
            )}
          </>
        )}
      </div>

      <div className="modal-footer">
        <button type="button" className="btn btn-secondary" onClick={onClose} disabled={isExporting}>
          انصراف
        </button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={handleConfirm}
          disabled={!columns || selected.size === 0 || isExporting}
        >
          {isExporting ? 'در حال آماده‌سازی...' : 'دانلود اکسل'}
        </button>
      </div>
    </Modal>
  )
}
