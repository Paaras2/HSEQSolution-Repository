import { toPersianDigits } from '../../lib/digits'

interface DonutSlice {
  label: string
  count: number
  color: string
}

interface DonutChartProps {
  slices: DonutSlice[]
  centerLabel: string
}

// نمودار دونات با SVG دستی. از stroke-dasharray روی یک دایره استفاده می‌کند (به‌جای
// محاسبه‌ی مسیر کمانی) - ساده‌تر و کم‌خطاتر است، و فاصله‌ی ۲px بین بخش‌ها به‌صورت
// طبیعی با کم‌کردن از طول هر قطعه ایجاد می‌شود.
export function DonutChart({ slices, centerLabel }: DonutChartProps) {
  const size = 180
  const strokeWidth = 26
  const radius = (size - strokeWidth) / 2
  const circumference = 2 * Math.PI * radius
  const gap = 2

  const total = slices.reduce((sum, s) => sum + s.count, 0)

  if (total === 0) {
    return <p className="text-muted">داده‌ای برای نمایش نیست.</p>
  }

  let accumulated = 0

  return (
    <div className="donut-chart-wrap">
      <svg
        className="donut-chart"
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        role="img"
        aria-label={`توزیع وضعیت بازبینی، مجموع ${toPersianDigits(total)}`}
      >
        <g transform={`rotate(-90 ${size / 2} ${size / 2})`}>
          {slices
            .filter((s) => s.count > 0)
            .map((s) => {
              const fraction = s.count / total
              const arcLength = Math.max(fraction * circumference - gap, 1)
              const offset = -accumulated * circumference
              accumulated += fraction
              return (
                <circle
                  key={s.label}
                  cx={size / 2}
                  cy={size / 2}
                  r={radius}
                  fill="none"
                  stroke={s.color}
                  strokeWidth={strokeWidth}
                  strokeDasharray={`${arcLength} ${circumference - arcLength}`}
                  strokeDashoffset={offset}
                >
                  <title>
                    {s.label}: {toPersianDigits(s.count)}
                  </title>
                </circle>
              )
            })}
        </g>

        <text className="donut-chart__total-value" x={size / 2} y={size / 2 - 2} textAnchor="middle">
          {toPersianDigits(total.toLocaleString('en-US'))}
        </text>
        <text className="donut-chart__total-label" x={size / 2} y={size / 2 + 16} textAnchor="middle">
          {centerLabel}
        </text>
      </svg>

      {/* راهنما همیشه هست - تشخیص هویت هر بخش نباید فقط به رنگ وابسته باشد. */}
      <div className="donut-legend">
        {slices.map((s) => (
          <div className="donut-legend__item" key={s.label}>
            <span className="donut-legend__dot" style={{ background: s.color }} />
            <span className="donut-legend__label">{s.label}</span>
            <span className="donut-legend__count">{toPersianDigits(s.count)}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
