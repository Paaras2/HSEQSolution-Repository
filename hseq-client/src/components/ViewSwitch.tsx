import { useRef, type KeyboardEvent, type ReactNode } from 'react'

// انتخابگرِ «نما» - چند گزینه‌ی ناسازگار که همیشه یکی‌شان فعال است.
//
// چرا رادیو و نه دکمه‌ی ساده: انتخابِ یکی از چند حالت است، نه اجرای یک کار. با
// role=radiogroup صفحه‌خوان «۱ از ۲» را می‌گوید و کاربر کیبورد با کلیدهای جهت بینشان
// می‌چرخد - رفتاری که از هر کنترلِ تب‌مانندی انتظار می‌رود.
export interface ViewSwitchOption<T extends string> {
  value: T
  label: string
  icon: ReactNode
  // توضیحِ کوتاهِ همان نما، روی tooltip. برچسبِ روی دکمه کوتاه می‌ماند و توضیح اینجا می‌آید.
  hint?: string
}

export function ViewSwitch<T extends string>({
  value,
  options,
  onChange,
  label,
}: {
  value: T
  options: ViewSwitchOption<T>[]
  onChange: (value: T) => void
  label: string
}) {
  // برای حرکت با کلیدهای جهت، خودِ دکمه‌ی مقصد باید فوکوس بگیرد.
  const buttons = useRef(new Map<T, HTMLButtonElement | null>())

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const index = options.findIndex((option) => option.value === value)
    if (index < 0) return

    // در RTL، «راست» یعنی گزینه‌ی قبلی. جهت از خودِ عنصر خوانده می‌شود تا این کنترل
    // در هر دو جهت درست رفتار کند، نه فقط در چیدمانِ فعلیِ برنامه.
    const rtl = getComputedStyle(event.currentTarget).direction === 'rtl'
    const steps: Partial<Record<string, number>> = {
      ArrowUp: -1,
      ArrowDown: 1,
      ArrowRight: rtl ? -1 : 1,
      ArrowLeft: rtl ? 1 : -1,
    }

    let next: number
    if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = options.length - 1
    else {
      const step = steps[event.key]
      if (step === undefined) return
      next = (index + step + options.length) % options.length
    }

    // جلوگیری از اسکرول صفحه با کلیدهای بالا/پایین.
    event.preventDefault()
    const target = options[next]
    onChange(target.value)
    buttons.current.get(target.value)?.focus()
  }

  return (
    <div className="view-switch" role="radiogroup" aria-label={label} onKeyDown={handleKeyDown}>
      {options.map((option) => {
        const isSelected = option.value === value
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={isSelected}
            // فقط گزینه‌ی فعال در نوبتِ Tab است؛ بینِ گزینه‌ها با کلیدهای جهت حرکت
            // می‌شود. این همان الگوی استانداردِ گروهِ رادیوست و Tab را شلوغ نمی‌کند.
            tabIndex={isSelected ? 0 : -1}
            className="view-switch__option"
            title={option.hint}
            ref={(node) => {
              buttons.current.set(option.value, node)
            }}
            onClick={() => onChange(option.value)}
          >
            <span className="view-switch__icon" aria-hidden="true">
              {option.icon}
            </span>
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
