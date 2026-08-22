import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { dashboardApi } from '../api/dashboardApi'
import { ApiError } from '../lib/httpClient'
import type { DashboardSummary, DocumentAlert } from '../types/api'
import { BarChart } from '../components/charts/BarChart'
import { HorizontalBarChart } from '../components/charts/HorizontalBarChart'
import { DonutChart } from '../components/charts/DonutChart'
import { LineChart } from '../components/charts/LineChart'
import { LoadingState, ErrorState, EmptyState } from '../components/StateViews'

// رنگ وضعیت بازبینی - عمداً از رنگ‌های وضعیت پروژه (نه پالت برند) استفاده می‌کند،
// چون این‌ها معنای خوب/هشدار/بحرانی دارند نه صرفاً «سری ۱، ۲، ۳».
const REVIEW_STATUS_COLORS = {
  onTrack: '#1f9d6b',
  dueSoon: '#e0a72a',
  overdue: '#d64545',
  noDateSet: '#b8bfc1',
}

// tone وضعیت فوریت را می‌رساند: danger برای منقضی‌شده، warning برای موعد نزدیک،
// و muted برای «بدون تاریخ» که یک شکاف اطلاعاتی است نه یک موعد از دست رفته.
function AlertSection({
  title,
  alerts,
  tone,
}: {
  title: string
  alerts: DocumentAlert[]
  tone: 'danger' | 'warning' | 'muted'
}) {
  return (
    <section className="card chart-card">
      <h2 className="create-card__title">{title}</h2>
      {alerts.length === 0 ? (
        <p className="text-muted" style={{ fontSize: 13, margin: 0 }}>
          موردی وجود ندارد.
        </p>
      ) : (
        <ul className="alert-list">
          {alerts.map((a) => (
            <li className="alert-row" key={a.key}>
              <span className="alert-row__info">
                <span className="alert-row__number mono">{a.number}</span>
                <span className="alert-row__name">{a.name}</span>
              </span>
              <span className={`alert-row__days alert-row__days--${tone}`}>
                {a.daysUntilDue === null
                  ? 'تعیین نشده'
                  : a.daysUntilDue < 0
                    ? `${Math.abs(a.daysUntilDue)} روز گذشته`
                    : `${a.daysUntilDue} روز مانده`}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
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
        <div>
          <h1>داشبورد</h1>
          <p>خوش آمدید، کد پرسنلی {user?.pcode}.</p>
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

      {!isLoading && !error && summary && (
        <>
          {/* ۱) کارت‌های KPI */}
          <div className="kpi-grid">
            <div className="card kpi-card">
              <div className="kpi-card__label">کل اسناد</div>
              <div className="kpi-card__value">{summary.totalDocuments.toLocaleString('en-US')}</div>
            </div>
            <div className="card kpi-card">
              <div className="kpi-card__label">اسناد فعال</div>
              <div className="kpi-card__value">{summary.activeDocuments.toLocaleString('en-US')}</div>
            </div>
            <div className="card kpi-card kpi-card--warning">
              <div className="kpi-card__label">نزدیک به بازبینی (۳۰ روز آینده)</div>
              <div className="kpi-card__value">{summary.dueForReview.toLocaleString('en-US')}</div>
            </div>
            <div className="card kpi-card kpi-card--danger">
              <div className="kpi-card__label">بازبینی‌های منقضی‌شده</div>
              <div className="kpi-card__value">{summary.overdueReviews.toLocaleString('en-US')}</div>
            </div>
          </div>

          {/* ۲) هشدارها - هر چهار در یک ردیف، هم‌تراز با چهار کارت KPI بالا */}
          <div className="dashboard-grid dashboard-grid--quarters">
            <AlertSection title="بازبینی منقضی‌شده" alerts={summary.expiredReviews} tone="danger" />
            <AlertSection title="بازبینی در ۷ روز آینده" alerts={summary.dueWithin7Days} tone="warning" />
            <AlertSection title="بازبینی در ۳۰ روز آینده" alerts={summary.dueWithin30Days} tone="warning" />
            <AlertSection title="بدون تاریخ بازبینی" alerts={summary.missingReviewDate} tone="muted" />
          </div>

          {/* ۳) نمودارها - دوتایی کنار هم؛ فقط نمودار روند (سری زمانی ۱۲ ماهه) عرض کامل می‌گیرد */}
          <div className="dashboard-grid">
            <section className="card chart-card">
              <h2 className="create-card__title">اسناد به تفکیک مدیریت سازمانی</h2>
              <p className="chart-card__hint">فقط آخرین بازنگری هر سند، و فقط اسناد فعال.</p>
              <BarChart data={summary.documentsByManagement} />
            </section>

            <section className="card chart-card">
              <h2 className="create-card__title">توزیع وضعیت بازبینی</h2>
              <p className="chart-card__hint">وضعیت هر سند فعال نسبت به موعد بازبینی‌اش.</p>
              <DonutChart
                centerLabel="سند فعال"
                slices={[
                  { label: 'در موعد', count: summary.reviewStatus.onTrack, color: REVIEW_STATUS_COLORS.onTrack },
                  { label: 'نزدیک به موعد', count: summary.reviewStatus.dueSoon, color: REVIEW_STATUS_COLORS.dueSoon },
                  { label: 'منقضی‌شده', count: summary.reviewStatus.overdue, color: REVIEW_STATUS_COLORS.overdue },
                  { label: 'بدون تاریخ بازبینی', count: summary.reviewStatus.noDateSet, color: REVIEW_STATUS_COLORS.noDateSet },
                ]}
              />
            </section>
          </div>

          <div className="dashboard-grid">
            <section className="card chart-card">
              <h2 className="create-card__title">اسناد به تفکیک فعالیت سازمانی</h2>
              <p className="chart-card__hint">۱۰ فعالیت پرتکرار.</p>
              <HorizontalBarChart data={summary.documentsByActivity} />
            </section>

            <section className="card chart-card">
              <h2 className="create-card__title">توزیع نوع سند</h2>
              <p className="chart-card__hint">۷ نوع پرتکرار، مابقی زیر «سایر».</p>
              <HorizontalBarChart data={summary.documentsByType} />
            </section>
          </div>

          <div className="dashboard-grid dashboard-grid--full">
            <section className="card chart-card">
              <h2 className="create-card__title">روند بازبینی‌های پیش‌رو</h2>
              <p className="chart-card__hint">تعداد اسنادی که در هر ماه موعد بازبینی دارند (۱۲ ماه آینده).</p>
              <LineChart data={summary.reviewTrend} />
            </section>
          </div>

          {summary.totalDocuments === 0 && (
            <div className="card">
              <EmptyState title="هنوز سندی ثبت نشده است." description="پس از افزودن اولین سند، آمار اینجا نمایش داده می‌شود." />
            </div>
          )}
        </>
      )}
    </div>
  )
}
