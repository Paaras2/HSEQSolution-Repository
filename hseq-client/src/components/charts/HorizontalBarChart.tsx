import type { NamedCount } from '../../types/api'

interface HorizontalBarChartProps {
  data: NamedCount[]
}

// نمودار میله‌ای افقی با HTML/CSS به‌جای SVG - برچسب فعالیت‌های سازمانی فارسی و طولانی
// است و در حالت افقی با متن معمولی (قابل انتخاب و با ellipsis خودکار) بهتر جا می‌شود.
export function HorizontalBarChart({ data }: HorizontalBarChartProps) {
  if (data.length === 0) {
    return <p className="text-muted">داده‌ای برای نمایش نیست.</p>
  }

  const maxValue = Math.max(...data.map((d) => d.count), 1)

  return (
    <div className="hbar-list">
      {data.map((d) => (
        <div className="hbar-row" key={d.code + d.label} title={`${d.label}: ${d.count}`}>
          <span className="hbar-row__label">{d.label}</span>
          <div className="hbar-row__track">
            <div className="hbar-row__fill" style={{ width: `${(d.count / maxValue) * 100}%` }} />
          </div>
          <span className="hbar-row__value">{d.count}</span>
        </div>
      ))}
    </div>
  )
}
