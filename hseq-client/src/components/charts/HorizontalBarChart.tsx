import type { NamedCount } from '../../types/api'
import { toPersianDigits } from '../../lib/digits'

interface HorizontalBarChartProps {
  data: NamedCount[]
  // نمایش کدِ کوتاه کنار عنوان. برای مدیریت سازمانی روشن است (کدش تک‌حرفی و آشناست)
  // و برای فهرست‌هایی که کدشان معنایی ندارد خاموش می‌ماند.
  showCode?: boolean
  // ته‌رنگ میله‌ها؛ هر نمودار رنگ خودش را می‌گیرد تا دو نمودارِ کنار هم قاطی نشوند.
  tone?: 'view' | 'edit' | 'revise' | 'history' | 'danger' | 'warning'
  // 'count' (پیش‌فرض): d.count تعداد است، سقفِ میله‌ها از بزرگ‌ترین مقدارِ همین فهرست
  // می‌آید. 'percent': d.count یک درصد است (۰ تا ۱۰۰)؛ سقف همیشه ثابت ۱۰۰ می‌ماند تا
  // میله‌ها روی مقیاسِ واقعی مقایسه شوند، نه نسبت به بزرگ‌ترینِ همین فهرست - وگرنه
  // «۶۲٪» و «۹۸٪» کنار هم تقریباً هم‌طول به نظر می‌رسیدند.
  valueFormat?: 'count' | 'percent'
}

// نمودار میله‌ای افقی با HTML/CSS به‌جای SVG - برچسب‌های فارسی طولانی‌اند و در حالت
// افقی با متن معمولی (قابل انتخاب و با ellipsis خودکار) بهتر جا می‌شوند.
export function HorizontalBarChart({ data, showCode = false, tone = 'revise', valueFormat = 'count' }: HorizontalBarChartProps) {
  if (data.length === 0) {
    return <p className="text-muted">داده‌ای برای نمایش نیست.</p>
  }

  const maxValue = valueFormat === 'percent' ? 100 : Math.max(...data.map((d) => d.count), 1)
  const formatValue = (count: number) => (valueFormat === 'percent' ? `٪${toPersianDigits(count)}` : toPersianDigits(count))

  return (
    <div className={`hbar-list hbar-list--${tone}`}>
      {data.map((d) => (
        <div className="hbar-row" key={d.code + d.label} title={`${d.label}: ${formatValue(d.count)}`}>
          <span className="hbar-row__label">
            {showCode && <span className="hbar-row__code mono">{toPersianDigits(d.code)}</span>}
            <span className="hbar-row__title">{d.label}</span>
          </span>
          <div className="hbar-row__track">
            <div className="hbar-row__fill" style={{ width: `${(d.count / maxValue) * 100}%` }} />
          </div>
          <span className="hbar-row__value">{formatValue(d.count)}</span>
        </div>
      ))}
    </div>
  )
}
