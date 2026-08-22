import { useEffect, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import {
  JALALI_MONTH_NAMES,
  JALALI_WEEKDAY_SHORT,
  gregorianToJalali,
  isoToJalaliText,
  jalaliMonthLength,
  jalaliTextToIso,
  jalaliToIso,
  jalaliWeekdayIndex,
  parseIsoDate,
  todayJalali,
} from '../lib/jalali'

interface PersianDatePickerProps {
  id: string
  // همیشه میلادی «yyyy-MM-dd» (یا رشته‌ی خالی) - همان قراردادی که input[type=date] داشت،
  // تا کد والد و آنچه به سرور می‌رود هیچ تغییری نکند.
  value: string
  onChange: (value: string) => void
  disabled?: boolean
}

// دیت‌پیکر شمسی: نمایش برای کاربر جلالی است، مقدار خروجی میلادی.
//
// عمداً <input type="date"> نیست، چون آن کنترل بومی مرورگر همیشه میلادی نشان می‌دهد و
// قابل شمسی‌کردن نیست. اینجا یک فیلد متنی + تقویم دستی ساخته شده تا بدون هیچ پکیجی
// نمایش شمسی داشته باشیم.
//
// ریشه <div> است نه <form> و Enter هم دستی مهار می‌شود، چون این کامپوننت داخل فرم
// «افزودن/ویرایش سند» رندر می‌شود و نباید آن فرم را زودتر از موعد ارسال کند.
export function PersianDatePicker({ id, value, onChange, disabled = false }: PersianDatePickerProps) {
  // متن قابل تایپ - از مقدار میلادیِ ورودی مشتق می‌شود، اما وقتی کاربر در حال تایپ است
  // دست‌نخورده می‌ماند تا کاراکترهای نیمه‌کاره پاک نشوند.
  const [text, setText] = useState(() => isoToJalaliText(value))
  const [isOpen, setIsOpen] = useState(false)
  const [isEditing, setIsEditing] = useState(false)

  const containerRef = useRef<HTMLDivElement>(null)

  // ماهی که تقویم روی آن باز است. با تغییر مقدار بیرونی هم‌گام می‌شود.
  const [viewMonth, setViewMonth] = useState(() => {
    const parsed = parseIsoDate(value)
    if (parsed) {
      const j = gregorianToJalali(parsed.gy, parsed.gm, parsed.gd)
      return { jy: j.jy, jm: j.jm }
    }
    const t = todayJalali()
    return { jy: t.jy, jm: t.jm }
  })

  // مقدار از بیرون عوض شود (مثلاً ریست فرم)، متن نمایش هم باید عوض شود - مگر وقتی
  // کاربر همان لحظه در حال تایپ کردن است.
  useEffect(() => {
    if (isEditing) return
    setText(isoToJalaliText(value))
  }, [value, isEditing])

  // بستن با کلیک بیرون یا Escape.
  useEffect(() => {
    if (!isOpen) return

    function handlePointerDown(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setIsOpen(false)
    }
    function handleKey(event: globalThis.KeyboardEvent) {
      if (event.key === 'Escape') setIsOpen(false)
    }

    document.addEventListener('mousedown', handlePointerDown)
    document.addEventListener('keydown', handleKey)
    return () => {
      document.removeEventListener('mousedown', handlePointerDown)
      document.removeEventListener('keydown', handleKey)
    }
  }, [isOpen])

  const selected = useMemo(() => {
    const parsed = parseIsoDate(value)
    return parsed ? gregorianToJalali(parsed.gy, parsed.gm, parsed.gd) : null
  }, [value])

  const today = useMemo(() => todayJalali(), [])

  // خانه‌های جدول ماه: چند خانه‌ی خالی برای شروع هفته، بعد روزهای ماه.
  const cells = useMemo(() => {
    const length = jalaliMonthLength(viewMonth.jy, viewMonth.jm)
    const firstIso = jalaliToIso(viewMonth.jy, viewMonth.jm, 1)
    const g = parseIsoDate(firstIso)!
    const offset = jalaliWeekdayIndex(g.gy, g.gm, g.gd)

    const result: (number | null)[] = []
    for (let i = 0; i < offset; i += 1) result.push(null)
    for (let d = 1; d <= length; d += 1) result.push(d)
    return result
  }, [viewMonth])

  // ثبت همان لحظه‌ی تایپ انجام می‌شود، نه فقط هنگام خروج از فیلد. اتکای انحصاری به blur
  // شکننده بود: اگر کاربر بعد از تایپ مستقیم روی «ذخیره» کلیک می‌کرد یا فرم با Enter
  // ارسال می‌شد، تاریخ تایپ‌شده ممکن بود اصلاً ثبت نشود.
  function handleTextChange(raw: string) {
    setIsEditing(true)
    setText(raw)

    const trimmed = raw.trim()
    // خالی کردن فیلد یعنی حذف تاریخ - این فیلدها اختیاری‌اند.
    if (!trimmed) {
      onChange('')
      return
    }

    // ورودی ناقص یا نامعتبر (مثل «۱۴۰۴/۱۲/۳۰» در سال غیرکبیسه) نادیده گرفته می‌شود؛
    // آخرین مقدار معتبر دست‌نخورده می‌ماند تا کاربر تایپش را تمام کند.
    const iso = jalaliTextToIso(trimmed)
    if (iso) onChange(iso)
  }

  // خروج از فیلد فقط نمایش را مرتب می‌کند: متن به شکل استاندارد آخرین مقدار معتبر
  // بازمی‌گردد، پس ورودی نامعتبر روی صفحه باقی نمی‌ماند.
  function handleBlur() {
    setIsEditing(false)
    setText(isoToJalaliText(value))
  }

  function pickDay(day: number) {
    onChange(jalaliToIso(viewMonth.jy, viewMonth.jm, day))
    setIsEditing(false)
    setIsOpen(false)
  }

  function shiftMonth(delta: number) {
    setViewMonth((current) => {
      const total = current.jy * 12 + (current.jm - 1) + delta
      return { jy: Math.floor(total / 12), jm: (total % 12) + 1 }
    })
  }

  // Enter داخل این فیلد باید تاریخ را ثبت کند، نه فرمِ بیرونی را ارسال کند.
  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault()
      handleBlur()
      setIsOpen(false)
    }
  }

  return (
    <div className="date-picker" ref={containerRef}>
      <div className="date-picker__control">
        <input
          id={id}
          className="text-input"
          value={text}
          onChange={(e) => handleTextChange(e.target.value)}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          onFocus={() => {
            // هم‌گام‌کردن ماهِ تقویم با مقدار فعلی، تا باز شدن از همان ماه شروع شود.
            const parsed = parseIsoDate(value)
            if (parsed) {
              const j = gregorianToJalali(parsed.gy, parsed.gm, parsed.gd)
              setViewMonth({ jy: j.jy, jm: j.jm })
            }
          }}
          placeholder="۱۴۰۴/۰۱/۰۱"
          inputMode="numeric"
          autoComplete="off"
          disabled={disabled}
        />
        <button
          type="button"
          className="date-picker__toggle"
          onClick={() => setIsOpen((open) => !open)}
          disabled={disabled}
          aria-label="باز کردن تقویم"
          aria-expanded={isOpen}
        >
          <span aria-hidden="true">📅</span>
        </button>
      </div>

      {isOpen && !disabled && (
        <div className="date-picker__popup" role="dialog" aria-label="انتخاب تاریخ">
          <div className="date-picker__header">
            {/* در چیدمان راست‌به‌چپ، «ماه قبل» سمت راست می‌نشیند. */}
            <button type="button" className="date-picker__nav" onClick={() => shiftMonth(-1)} aria-label="ماه قبل">
              ›
            </button>
            <span className="date-picker__month">
              {JALALI_MONTH_NAMES[viewMonth.jm - 1]} {viewMonth.jy}
            </span>
            <button type="button" className="date-picker__nav" onClick={() => shiftMonth(1)} aria-label="ماه بعد">
              ‹
            </button>
          </div>

          <div className="date-picker__weekdays">
            {JALALI_WEEKDAY_SHORT.map((day) => (
              <span key={day}>{day}</span>
            ))}
          </div>

          <div className="date-picker__grid">
            {cells.map((day, index) => {
              if (day === null) return <span key={`empty-${index}`} className="date-picker__cell is-empty" />

              const isSelected =
                selected != null &&
                selected.jy === viewMonth.jy &&
                selected.jm === viewMonth.jm &&
                selected.jd === day
              const isToday = today.jy === viewMonth.jy && today.jm === viewMonth.jm && today.jd === day

              return (
                <button
                  key={day}
                  type="button"
                  className={`date-picker__cell${isSelected ? ' is-selected' : ''}${isToday ? ' is-today' : ''}`}
                  onClick={() => pickDay(day)}
                >
                  {day}
                </button>
              )
            })}
          </div>

          <div className="date-picker__footer">
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={() => {
                onChange(jalaliToIso(today.jy, today.jm, today.jd))
                setViewMonth({ jy: today.jy, jm: today.jm })
                setIsEditing(false)
                setIsOpen(false)
              }}
            >
              امروز
            </button>
            <button
              type="button"
              className="btn btn-ghost btn-sm"
              onClick={() => {
                onChange('')
                setText('')
                setIsEditing(false)
                setIsOpen(false)
              }}
            >
              پاک کردن
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
