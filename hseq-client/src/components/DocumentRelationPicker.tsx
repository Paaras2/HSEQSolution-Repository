import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { searchApi } from '../api/searchApi'
import { DOCUMENT_RELATION_TYPE_OPTIONS } from '../types/api'
import type { DocumentRelationTypeValue, DocumentSuggestion } from '../types/api'
import { toPersianDigits } from '../lib/digits'

const SUGGEST_DEBOUNCE_MS = 300

export interface PickedRelation {
  target: DocumentSuggestion
  relationType: DocumentRelationTypeValue
  note: string | null
}

interface DocumentRelationPickerProps {
  // کلید مدارکی که نباید پیشنهاد شوند: خودِ مدرک جاری، و هر مدرکی که از قبل مرتبط
  // (یا در فرم افزودن، از قبل انتخاب) شده. سرور هم این دو را رد می‌کند؛ این فیلتر فقط
  // جلوی پیشنهاد گزینه‌ای را می‌گیرد که انتخابش قطعاً به خطا می‌خورد.
  excludedDocumentIds: string[]
  disabled?: boolean
  isSubmitting: boolean
  // خطای والد (مثلاً پاسخ سرور). خطای «مدرکی انتخاب نشده» را خود این کامپوننت می‌سازد.
  error: string | null
  submitLabel: string
  submittingLabel: string
  // true یعنی انتخاب پذیرفته شد و فرم باید پاک شود. false یعنی والد ردش کرد و
  // ورودی‌ها باید سر جایشان بمانند تا کاربر اصلاحشان کند.
  onPick: (picked: PickedRelation) => Promise<boolean>
}

// انتخابگر مشترک «یک مدرک + نوع ارتباط + توضیح». دو مصرف‌کننده دارد که کار متفاوتی با
// نتیجه می‌کنند: RelatedDocumentsDrawer بلافاصله روی سرور ثبتش می‌کند، و DocumentCreatePage
// در صف نگه می‌دارد تا سند ساخته شود و کلید بگیرد. برای همین خودش چیزی را ثبت نمی‌کند و
// فقط انتخاب را به والد می‌دهد.
export function DocumentRelationPicker({
  excludedDocumentIds,
  disabled = false,
  isSubmitting,
  error,
  submitLabel,
  submittingLabel,
  onPick,
}: DocumentRelationPickerProps) {
  const [query, setQuery] = useState('')
  const [suggestions, setSuggestions] = useState<DocumentSuggestion[]>([])
  const [showSuggestions, setShowSuggestions] = useState(false)
  const [target, setTarget] = useState<DocumentSuggestion | null>(null)
  const [relationType, setRelationType] = useState<DocumentRelationTypeValue>(0)
  const [note, setNote] = useState('')
  const [localError, setLocalError] = useState<string | null>(null)

  const suggestTimer = useRef<number | null>(null)
  // فهرست کنارگذاشته‌ها هنگام رسیدن پاسخ خوانده می‌شود، نه هنگام ارسال درخواست - وگرنه
  // تایپِ پیش از ثبتِ یک ارتباط، با فهرست کهنه فیلتر می‌شد.
  const excludedRef = useRef(excludedDocumentIds)
  excludedRef.current = excludedDocumentIds

  useEffect(() => {
    return () => {
      if (suggestTimer.current) window.clearTimeout(suggestTimer.current)
    }
  }, [])

  function reset() {
    setQuery('')
    setTarget(null)
    setSuggestions([])
    setShowSuggestions(false)
    setRelationType(0)
    setNote('')
  }

  function handleQueryChange(value: string) {
    setQuery(value)
    // تایپ دوباره یعنی انتخاب قبلی دیگر معتبر نیست؛ وگرنه کاربر می‌توانست عبارت را
    // عوض کند و همچنان مدرک قبلی ثبت شود.
    setTarget(null)
    setLocalError(null)

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
          const excluded = new Set(excludedRef.current)
          const usable = items.filter((s) => !excluded.has(s.key))
          setSuggestions(usable)
          setShowSuggestions(usable.length > 0)
        })
        .catch(() => {
          /* پیشنهاد خودکار صرفاً کمکی است - شکستش نباید به کاربر نشان داده شود. */
        })
    }, SUGGEST_DEBOUNCE_MS)
  }

  function handleSuggestionPick(suggestion: DocumentSuggestion) {
    setTarget(suggestion)
    setQuery(suggestion.number + ' - ' + suggestion.name)
    setShowSuggestions(false)
  }

  // عمداً <form> نیست و این هم دستی مدیریت می‌شود: این کامپوننت داخل فرم صفحه‌ی افزودن سند
  // رندر می‌شود و فرمِ تودرتو هم HTML نامعتبر است و هم ریسک این را دارد که Enter فرمِ بیرونی
  // را بفرستد - یعنی سند پیش از تکمیل انتخاب ارتباط ذخیره شود.
  async function handleSubmit() {
    if (isSubmitting || disabled) return

    if (!target) {
      setLocalError('ابتدا مدرک مقصد را از فهرست پیشنهادها انتخاب کنید.')
      return
    }

    setLocalError(null)
    const accepted = await onPick({ target, relationType, note: note.trim() || null })
    if (accepted) reset()
  }

  const isLocked = disabled || isSubmitting

  // Enter داخل فیلدهای این بخش باید همین انتخاب را ثبت کند، نه فرمِ بیرونی را.
  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== 'Enter') return
    event.preventDefault()
    void handleSubmit()
  }

  return (
    <div className="relation-add" onKeyDown={handleKeyDown}>
      <div className="form-grid">
        <div className="field span-2 relation-add__picker">
          <label htmlFor="relation-target">مدرک مقصد</label>
          <input
            id="relation-target"
            className="text-input"
            value={query}
            onChange={(e) => handleQueryChange(e.target.value)}
            onFocus={() => setShowSuggestions(suggestions.length > 0)}
            onBlur={() => window.setTimeout(() => setShowSuggestions(false), 150)}
            placeholder="شماره یا بخشی از عنوان مدرک..."
            autoComplete="off"
            disabled={isLocked}
          />
          {showSuggestions && (
            <ul className="suggestion-list">
              {suggestions.map((s) => (
                <li key={s.key}>
                  <button type="button" onMouseDown={() => handleSuggestionPick(s)}>
                    <span className="mono">{toPersianDigits(s.number)}</span>
                    <span>{s.name}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <p className="field-hint">
            {target ? (
              <>
                انتخاب‌شده: <span className="mono">{toPersianDigits(target.number)}</span>
              </>
            ) : (
              'حداقل ۲ کاراکتر بنویسید و مدرک را از فهرست پیشنهادها انتخاب کنید.'
            )}
          </p>
        </div>

        <div className="field">
          <label htmlFor="relation-type">نوع ارتباط</label>
          <select
            id="relation-type"
            className="select-input"
            value={relationType}
            onChange={(e) => setRelationType(Number(e.target.value) as DocumentRelationTypeValue)}
            disabled={isLocked}
          >
            {DOCUMENT_RELATION_TYPE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="relation-note">توضیح (اختیاری)</label>
          <input
            id="relation-note"
            className="text-input"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={500}
            placeholder="مثلاً: فرم ثبت بازرسیِ این دستورالعمل"
            disabled={isLocked}
          />
        </div>
      </div>

      {(localError || error) && (
        <div className="form-error" role="alert">
          {localError ?? error}
        </div>
      )}

      <div className="relation-add__actions">
        <button
          type="button"
          className="btn btn-primary btn-sm"
          onClick={() => void handleSubmit()}
          disabled={isLocked || !target}
        >
          {isSubmitting ? submittingLabel : submitLabel}
        </button>
      </div>
    </div>
  )
}
