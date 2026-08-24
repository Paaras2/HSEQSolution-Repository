import type { NamedCount } from '../../types/api'
import { toPersianDigits } from '../../lib/digits'

interface BarChartProps {
  data: NamedCount[]
}

// مسیر یک مستطیل با گوشه‌های گرد فقط بالا (نوک میله) - پایه‌ی میله (خط پایه) گوشه‌تیز
// می‌ماند، طبق قاعده‌ی «۴px گرد در نوک، تیز در خط پایه».
function roundedTopRectPath(x: number, y: number, w: number, h: number, r: number): string {
  const radius = Math.max(0, Math.min(r, w / 2, h))
  return `M${x},${y + h} L${x},${y + radius} Q${x},${y} ${x + radius},${y} L${x + w - radius},${y} Q${x + w},${y} ${x + w},${y + radius} L${x + w},${y + h} Z`
}

// نمودار میله‌ای عمودی ساده (SVG دستی، بدون هیچ کتابخانه‌ای). یک رنگ ثابت برای همه‌ی
// میله‌ها - چون هر میله فقط یک دسته است نه یک سری جدا، رنگ‌کردن هرکدام با رنگ متفاوت
// چیزی اضافه نمی‌کرد و فقط شلوغ می‌شد.
export function BarChart({ data }: BarChartProps) {
  const height = 260
  const paddingTop = 24
  const paddingBottom = 40
  const paddingStart = 8
  const paddingEnd = 44

  if (data.length === 0) {
    return <p className="text-muted">داده‌ای برای نمایش نیست.</p>
  }

  // عرض از روی تعداد دسته‌ها ساخته می‌شود، نه ثابت. با عرض ثابت، دو دسته روی کل عرض
  // کارت کشیده می‌شدند و بین دو میله‌ی باریک فضای خالی بزرگی می‌ماند.
  const slotWidthPx = 72
  const width = paddingStart + paddingEnd + data.length * slotWidthPx

  // همان منطق تیک‌های صحیح و بدون تکرارِ LineChart - گام را گرد می‌کنیم، بعد سقف را
  // روی مضربی از آن می‌بریم.
  const maxValue = Math.max(...data.map((d) => d.count), 1)
  const gridSteps = 4
  const step = Math.max(1, Math.ceil((maxValue * 1.15) / gridSteps))
  const niceMax = step * gridSteps
  const plotWidth = width - paddingStart - paddingEnd
  const plotHeight = height - paddingTop - paddingBottom
  const slotWidth = plotWidth / data.length
  const barWidth = Math.min(24, slotWidth * 0.55)

  const gridValues = Array.from({ length: gridSteps + 1 }, (_, i) => step * i)

  return (
    <svg
      className="bar-chart"
      viewBox={`0 0 ${width} ${height}`}
      style={{ width: `${width}px` }}
      role="img"
      aria-label="نمودار اسناد به تفکیک مدیریت سازمانی"
    >
      {gridValues.map((value) => {
        const y = paddingTop + plotHeight - (value / niceMax) * plotHeight
        return (
          <g key={value}>
            <line className="bar-chart__gridline" x1={paddingStart} x2={width - paddingEnd} y1={y} y2={y} />
            {/* برچسب محور در حاشیه‌ی سمت راست می‌نشیند، نه روی خودِ نمودار */}
            <text className="bar-chart__axis-label" x={width - paddingEnd + 8} y={y + 4} textAnchor="start">
              {toPersianDigits(value.toLocaleString('en-US'))}
            </text>
          </g>
        )
      })}

      {data.map((d, i) => {
        const barHeight = (d.count / niceMax) * plotHeight
        const x = paddingStart + i * slotWidth + (slotWidth - barWidth) / 2
        const y = paddingTop + plotHeight - barHeight
        return (
          <g key={d.code}>
            <title>
              {d.label}: {toPersianDigits(d.count)}
            </title>
            <path className="bar-chart__bar" d={roundedTopRectPath(x, y, barWidth, Math.max(barHeight, 1), 4)} />
            {barHeight > 14 && (
              <text className="bar-chart__value-label" x={x + barWidth / 2} y={y - 6} textAnchor="middle">
                {toPersianDigits(d.count)}
              </text>
            )}
            <text
              className="bar-chart__axis-label"
              x={x + barWidth / 2}
              y={height - paddingBottom + 16}
              textAnchor="middle"
            >
              {d.code}
            </text>
          </g>
        )
      })}
    </svg>
  )
}
