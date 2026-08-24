import { useState } from 'react'
import type { MonthCount } from '../../types/api'
import { toPersianDigits } from '../../lib/digits'

interface LineChartProps {
  data: MonthCount[]
}

// نمودار خطی با SVG دستی + tooltip روی هاور نقاط. برچسب محور افقی یک‌درمیان نمایش
// داده می‌شود تا ۱۲ برچسب ماه روی هم نیفتند.
export function LineChart({ data }: LineChartProps) {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)

  // نسبت عرض به ارتفاع عمداً پهن است: نمودار عرض کامل می‌گیرد و با viewBox باریک‌تر،
  // ارتفاع رندرشده بی‌دلیل بلند می‌شد (۹۴۰×۳۵۰ به‌جای ۹۴۰×۲۵۵).
  const width = 960
  const height = 260
  const paddingTop = 20
  const paddingBottom = 36
  const paddingStart = 36
  const paddingEnd = 12

  if (data.length === 0) {
    return <p className="text-muted">داده‌ای برای نمایش نیست.</p>
  }

  // تیک‌های محور عمودی همیشه عدد صحیح و بدون تکرارند: ابتدا گام را گرد می‌کنیم، بعد
  // سقف را روی مضربی از همان گام می‌بریم. بدون این کار، برای مقادیر کوچک (مثلاً حداکثر ۱)
  // تیک‌ها بعد از گردکردن تکراری می‌شدند (۰، ۱، ۱، ۲، ۲).
  const maxValue = Math.max(...data.map((d) => d.count), 1)
  const gridSteps = 4
  const step = Math.max(1, Math.ceil((maxValue * 1.2) / gridSteps))
  const niceMax = step * gridSteps
  const plotWidth = width - paddingStart - paddingEnd
  const plotHeight = height - paddingTop - paddingBottom

  const pointAt = (index: number, value: number) => ({
    x: paddingStart + (data.length === 1 ? plotWidth / 2 : (index / (data.length - 1)) * plotWidth),
    y: paddingTop + plotHeight - (value / niceMax) * plotHeight,
  })

  const points = data.map((d, i) => pointAt(i, d.count))
  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x},${p.y}`).join(' ')
  const areaPath = `${linePath} L${points[points.length - 1].x},${paddingTop + plotHeight} L${points[0].x},${paddingTop + plotHeight} Z`

  const gridValues = Array.from({ length: gridSteps + 1 }, (_, i) => step * i)

  return (
    <div className="line-chart-wrap">
      <svg
        className="line-chart"
        viewBox={`0 0 ${width} ${height}`}
        role="img"
        aria-label="روند موعد بازبینی اسناد در ۱۲ ماه آینده"
      >
        {gridValues.map((value) => {
          const y = paddingTop + plotHeight - (value / niceMax) * plotHeight
          return (
            <g key={value}>
              <line className="line-chart__gridline" x1={paddingStart} x2={width - paddingEnd} y1={y} y2={y} />
              <text className="line-chart__axis-label" x={paddingStart - 8} y={y + 4} textAnchor="end">
                {toPersianDigits(value)}
              </text>
            </g>
          )
        })}

        <path className="line-chart__area" d={areaPath} />
        <path className="line-chart__line" d={linePath} />

        {points.map((p, i) => (
          <circle
            key={data[i].monthLabel}
            className={`line-chart__dot${hoverIndex === i ? ' line-chart__dot--hover' : ''}`}
            cx={p.x}
            cy={p.y}
            r={hoverIndex === i ? 6 : 4}
            onMouseEnter={() => setHoverIndex(i)}
            onMouseLeave={() => setHoverIndex(null)}
          />
        ))}

        {data.map((d, i) =>
          i % 2 === 0 ? (
            <text
              key={d.monthLabel}
              className="line-chart__axis-label"
              x={points[i].x}
              y={height - paddingBottom + 18}
              textAnchor="middle"
            >
              {toPersianDigits(d.monthLabel.slice(2))}
            </text>
          ) : null,
        )}
      </svg>

      {hoverIndex !== null && (
        <div
          className="line-chart-tooltip"
          style={{
            insetInlineStart: `${(points[hoverIndex].x / width) * 100}%`,
            top: `${(points[hoverIndex].y / height) * 100}%`,
          }}
        >
          {toPersianDigits(data[hoverIndex].monthLabel)} — {toPersianDigits(data[hoverIndex].count)} سند
        </div>
      )}
    </div>
  )
}
