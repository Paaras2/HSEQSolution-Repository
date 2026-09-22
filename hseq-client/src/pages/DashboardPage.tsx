import { useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { dashboardApi } from '../api/dashboardApi'
import { ApiError } from '../lib/httpClient'
import type { DashboardSummary } from '../types/api'
import { HorizontalBarChart } from '../components/charts/HorizontalBarChart'
import { LoadingState, ErrorState, EmptyState } from '../components/StateViews'
import { toPersianDigits } from '../lib/digits'
import { CalendarIcon, CheckIcon, DeactivateIcon, FileTextIcon } from '../components/Icons'

// کارت KPI با آیکون رنگی. value از قبل به‌صورت متنِ نمایشی (رقم فارسی، یا با ٪)
// آماده می‌رسد - این کامپوننت فقط چیدمان می‌دهد، قالب‌بندیِ عدد کارِ فراخوان است.
function KpiCard({
  label,
  value,
  icon,
  tone,
}: {
  label: string
  value: string
  icon: ReactNode
  tone: 'view' | 'success' | 'warning' | 'danger'
}) {
  return (
    <div className={`kpi-card kpi-card--${tone}`}>
      <span className="kpi-card__icon">{icon}</span>
      <span className="kpi-card__body">
        <span className="kpi-card__value">{value}</span>
        <span className="kpi-card__label">{label}</span>
      </span>
    </div>
  )
}

// درصدِ به‌روزبودن به رنگِ وضعیت ترجمه می‌شود: بالای ۸۰٪ خوب، ۵۰ تا ۸۰٪ هشدار، زیر
// آن بحرانی - همان آستانه‌هایی که یک ممیز کیفیت برای «آیا کنترل اسناد کار می‌کند؟» به کار می‌برد.
function percentUpToDateTone(percent: number): 'success' | 'warning' | 'danger' {
  if (percent >= 80) return 'success'
  if (percent >= 50) return 'warning'
  return 'danger'
}

export function DashboardPage() {
  const { user, hasCapability } = useAuth()
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    setIsLoading(true)
    setError(null)
    dashboardApi
      .getSummary()
      .then(setSummary)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'امکان بارگذاری داشبورد وجود ندارد.'))
      .finally(() => setIsLoading(false))
  }, [])

  useEffect(load, [load])

  return (
    <div>
      <div className="page-header">
        <div className="page-header__text">
          <h1>داشبورد</h1>
          <p>خوش آمدید، کد پرسنلی {toPersianDigits(user?.pcode)}</p>
        </div>
        <Link to="/documents" className="btn btn-primary">
          {hasCapability('documents:manage') ? 'مدیریت اسناد' : 'مشاهده اسناد'}
        </Link>
      </div>

      {isLoading && (
        <div className="card">
          <LoadingState title="در حال بارگذاری داشبورد..." />
        </div>
      )}

      {!isLoading && error && (
        <div className="card">
          <ErrorState
            title={error}
            action={
              <button type="button" className="btn btn-secondary btn-sm" onClick={load}>
                تلاش مجدد
              </button>
            }
          />
        </div>
      )}

      {!isLoading && !error && summary && summary.totalDocuments === 0 && (
        <div className="card">
          <EmptyState
            title="هنوز سندی ثبت نشده است."
            description="پس از افزودن اولین سند، آمار اینجا نمایش داده می‌شود."
          />
        </div>
      )}

      {!isLoading && !error && summary && summary.totalDocuments > 0 && (
        <>
          {/* ۱) چهار عددِ اصلی. «درصد اسناد به‌روز» تنها عددی است که کل وضعیتِ کنترلِ
              اسناد را در یک نگاه می‌گوید - رنگش با همان عدد عوض می‌شود. */}
          <div className="kpi-grid">
            <KpiCard
              label="کل اسناد فعال"
              value={toPersianDigits(summary.totalDocuments.toLocaleString('en-US'))}
              icon={<FileTextIcon size={20} />}
              tone="view"
            />
            <KpiCard
              label="اسناد به‌روز"
              value={`٪${toPersianDigits(Math.round(summary.percentUpToDate))}`}
              icon={<CheckIcon size={20} />}
              tone={percentUpToDateTone(summary.percentUpToDate)}
            />
            <KpiCard
              label="بازبینی معوق"
              value={toPersianDigits(summary.overdueReviews.toLocaleString('en-US'))}
              icon={<DeactivateIcon size={20} />}
              tone="danger"
            />
            <KpiCard
              label="بدون تاریخ بازبینی"
              value={toPersianDigits(summary.missingReviewDateCount.toLocaleString('en-US'))}
              icon={<CalendarIcon size={20} />}
              tone="warning"
            />
          </div>

          {/* ۲) بار معوقات: سن (چقدر عقب‌افتاده) و مدیریتِ صاحبِ آن - این دو نمودار
              خودِ عددِ «معوق» بالا را می‌شکافند، نه صرفاً یک ترکیب‌بندیِ خنثی. */}
          <div className="dashboard-section">
            <header className="dashboard-section__head">
              <h2 className="dashboard-section__title">بار معوقاتِ بازبینی</h2>
              <p className="dashboard-section__hint">این معوقات چقدر قدیمی‌اند و بیشتر روی دوشِ کدام مدیریت‌اند.</p>
            </header>
            <div className="dashboard-grid">
              <section className="card chart-card">
                <h3 className="chart-card__title">سنِ معوقات</h3>
                <p className="chart-card__hint">چند وقت از موعدِ بازبینی گذشته - هرچه قدیمی‌تر، اقدام سخت‌تر.</p>
                <HorizontalBarChart data={summary.overdueByAge} tone="danger" />
              </section>

              <section className="card chart-card">
                <h3 className="chart-card__title">سهمِ معوق در هر مدیریت</h3>
                <p className="chart-card__hint">چند درصد از اسنادِ هر مدیریت معوق است، نه صرفاً تعداد خام.</p>
                <HorizontalBarChart data={summary.overdueByManagement} showCode tone="warning" valueFormat="percent" />
              </section>
            </div>
          </div>

          {/* ۳) ترکیبِ اسناد - زمینه‌ای برای شناخت کلی، نه یک اقدامِ فوری؛ برای همین
              آخرِ صفحه و کم‌رنگ‌تر از بخش‌های بالا. */}
          <div className="dashboard-section">
            <header className="dashboard-section__head">
              <h2 className="dashboard-section__title">ترکیبِ اسناد</h2>
            </header>
            <div className="dashboard-grid">
              <section className="card chart-card">
                <h3 className="chart-card__title">اسناد به تفکیک فعالیت سازمانی</h3>
                <p className="chart-card__hint">۱۰ فعالیت پرتکرار.</p>
                <HorizontalBarChart data={summary.documentsByActivity} tone="revise" />
              </section>

              <section className="card chart-card">
                <h3 className="chart-card__title">توزیع نوع سند</h3>
                <p className="chart-card__hint">۷ نوع پرتکرار، مابقی زیر «سایر».</p>
                <HorizontalBarChart data={summary.documentsByType} tone="edit" />
              </section>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
