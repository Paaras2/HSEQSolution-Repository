import { useCallback, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { dashboardApi } from '../api/dashboardApi'
import { ApiError } from '../lib/httpClient'
import type { DashboardSummary, DocumentAlert } from '../types/api'
import { HorizontalBarChart } from '../components/charts/HorizontalBarChart'
import { DonutChart } from '../components/charts/DonutChart'
import { LineChart } from '../components/charts/LineChart'
import { LoadingState, ErrorState, EmptyState } from '../components/StateViews'
import { toPersianDigits } from '../lib/digits'
import {
  AlertTriangleIcon,
  CalendarIcon,
  ChartIcon,
  CheckIcon,
  DashboardIcon,
  DeactivateIcon,
  FileTextIcon,
} from '../components/Icons'

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
    <section className="alert-card">
      <header className="alert-card__head">
        <h3>{title}</h3>
        <span className={`alert-card__count alert-card__count--${tone}`}>{toPersianDigits(alerts.length)}</span>
      </header>
      {alerts.length === 0 ? (
        <p className="alert-card__empty">موردی وجود ندارد.</p>
      ) : (
        <ul className="alert-list">
          {alerts.map((a) => (
            <li className="alert-row" key={a.key}>
              <span className="alert-row__info">
                <span className="alert-row__number mono">{toPersianDigits(a.number)}</span>
                <span className="alert-row__name">{a.name}</span>
              </span>
              <span className={`alert-row__days alert-row__days--${tone}`}>
                {a.daysUntilDue === null
                  ? 'تعیین نشده'
                  : a.daysUntilDue < 0
                    ? `${toPersianDigits(Math.abs(a.daysUntilDue))} روز گذشته`
                    : `${toPersianDigits(a.daysUntilDue)} روز مانده`}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

// کارت KPI با آیکون رنگی - همان زبان بصریِ ناوبری پنل ادمین.
function KpiCard({
  label,
  value,
  icon,
  tone,
}: {
  label: string
  value: number
  icon: ReactNode
  tone: 'view' | 'success' | 'warning' | 'danger'
}) {
  return (
    <div className={`kpi-card kpi-card--${tone}`}>
      <span className="kpi-card__icon">{icon}</span>
      <span className="kpi-card__body">
        <span className="kpi-card__value">{toPersianDigits(value.toLocaleString('en-US'))}</span>
        <span className="kpi-card__label">{label}</span>
      </span>
    </div>
  )
}

// سه بخشِ داشبورد. به‌جای اینکه همه‌ی نمودارها و هشدارها زیر هم بنشینند و صفحه چند
// برابر ارتفاع پنجره شود، هر بخش تقریباً یک صفحه است و با تب عوض می‌شود.
type Section = 'overview' | 'breakdown' | 'alerts'

export function DashboardPage() {
  const { user, hasCapability } = useAuth()
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [section, setSection] = useState<Section>('overview')

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

  // شمارنده‌ی هشدارهای فوری روی تب. پنهان کردن «بازبینی منقضی‌شده» پشت یک تب فقط وقتی
  // بی‌خطر است که کاربر بدون باز کردنش هم بفهمد چند مورد منتظر اوست.
  const urgentCount = summary
    ? summary.expiredReviews.length + summary.dueWithin7Days.length
    : 0

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>داشبورد</h1>
          <p>خوش آمدید، کد پرسنلی {toPersianDigits(user?.pcode)}.</p>
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
          {/* ۱) چهار عدد اصلی، همیشه دیده می‌شوند - مستقل از تبِ انتخاب‌شده. */}
          <div className="kpi-grid">
            <KpiCard
              label="کل اسناد"
              value={summary.totalDocuments}
              icon={<FileTextIcon size={20} />}
              tone="view"
            />
            <KpiCard
              label="اسناد فعال"
              value={summary.activeDocuments}
              icon={<CheckIcon size={20} />}
              tone="success"
            />
            <KpiCard
              label="نزدیک به بازبینی (۳۰ روز)"
              value={summary.dueForReview}
              icon={<CalendarIcon size={20} />}
              tone="warning"
            />
            <KpiCard
              label="بازبینی‌های منقضی‌شده"
              value={summary.overdueReviews}
              icon={<DeactivateIcon size={20} />}
              tone="danger"
            />
          </div>

          {/* ۲) ناوبری بخش‌ها - همان الگوی آیکون رنگیِ پنل ادمین. */}
          <nav className="section-nav" aria-label="بخش‌های داشبورد">
            <button
              type="button"
              className={`section-nav__item section-nav__item--view${section === 'overview' ? ' is-active' : ''}`}
              onClick={() => setSection('overview')}
              aria-current={section === 'overview' ? 'page' : undefined}
            >
              <span className="section-nav__icon">
                <DashboardIcon size={17} />
              </span>
              <span className="section-nav__label">نمای کلی</span>
            </button>

            <button
              type="button"
              className={`section-nav__item section-nav__item--edit${section === 'breakdown' ? ' is-active' : ''}`}
              onClick={() => setSection('breakdown')}
              aria-current={section === 'breakdown' ? 'page' : undefined}
            >
              <span className="section-nav__icon">
                <ChartIcon size={17} />
              </span>
              <span className="section-nav__label">تفکیک و روند</span>
            </button>

            <button
              type="button"
              className={`section-nav__item section-nav__item--history${section === 'alerts' ? ' is-active' : ''}`}
              onClick={() => setSection('alerts')}
              aria-current={section === 'alerts' ? 'page' : undefined}
            >
              <span className="section-nav__icon">
                <AlertTriangleIcon size={17} />
              </span>
              <span className="section-nav__label">هشدارها</span>
              {/* نشانِ قرمز، تعداد موارد فوری را بدون باز کردن تب می‌گوید. */}
              {urgentCount > 0 && <span className="section-nav__badge">{toPersianDigits(urgentCount)}</span>}
            </button>
          </nav>

          {/* ۳) محتوای بخش انتخاب‌شده */}
          {section === 'overview' && (
            <div className="dashboard-grid">
              <section className="card chart-card">
                <h2 className="chart-card__title">اسناد به تفکیک مدیریت سازمانی</h2>
                <p className="chart-card__hint">فقط آخرین بازنگری هر سند، و فقط اسناد فعال.</p>
                {/* افقی، نه عمودی: در حالت عمودی زیر هر میله فقط کدِ تک‌حرفی جا می‌شد و
                    نام مدیریت اصلاً دیده نمی‌شد. */}
                <HorizontalBarChart data={summary.documentsByManagement} showCode tone="view" />
              </section>

              <section className="card chart-card">
                <h2 className="chart-card__title">توزیع وضعیت بازبینی</h2>
                <p className="chart-card__hint">وضعیت هر سند فعال نسبت به موعد بازبینی‌اش.</p>
                <DonutChart
                  centerLabel="سند فعال"
                  slices={[
                    { label: 'در موعد', count: summary.reviewStatus.onTrack, color: REVIEW_STATUS_COLORS.onTrack },
                    { label: 'نزدیک به موعد', count: summary.reviewStatus.dueSoon, color: REVIEW_STATUS_COLORS.dueSoon },
                    { label: 'منقضی‌شده', count: summary.reviewStatus.overdue, color: REVIEW_STATUS_COLORS.overdue },
                    {
                      label: 'بدون تاریخ بازبینی',
                      count: summary.reviewStatus.noDateSet,
                      color: REVIEW_STATUS_COLORS.noDateSet,
                    },
                  ]}
                />
              </section>
            </div>
          )}

          {section === 'breakdown' && (
            <>
              <div className="dashboard-grid">
                <section className="card chart-card">
                  <h2 className="chart-card__title">اسناد به تفکیک فعالیت سازمانی</h2>
                  <p className="chart-card__hint">۱۰ فعالیت پرتکرار.</p>
                  <HorizontalBarChart data={summary.documentsByActivity} tone="revise" />
                </section>

                <section className="card chart-card">
                  <h2 className="chart-card__title">توزیع نوع سند</h2>
                  <p className="chart-card__hint">۷ نوع پرتکرار، مابقی زیر «سایر».</p>
                  <HorizontalBarChart data={summary.documentsByType} tone="edit" />
                </section>
              </div>

              <div className="dashboard-grid dashboard-grid--full">
                <section className="card chart-card">
                  <h2 className="chart-card__title">روند بازبینی‌های پیش‌رو</h2>
                  <p className="chart-card__hint">تعداد اسنادی که در هر ماه موعد بازبینی دارند (۱۲ ماه آینده).</p>
                  <LineChart data={summary.reviewTrend} />
                </section>
              </div>
            </>
          )}

          {section === 'alerts' && (
            <div className="card alerts-panel">
              <div className="alerts-panel__grid">
                <AlertSection title="بازبینی منقضی‌شده" alerts={summary.expiredReviews} tone="danger" />
                <AlertSection title="بازبینی در ۷ روز آینده" alerts={summary.dueWithin7Days} tone="warning" />
                <AlertSection title="بازبینی در ۳۰ روز آینده" alerts={summary.dueWithin30Days} tone="warning" />
                <AlertSection title="بدون تاریخ بازبینی" alerts={summary.missingReviewDate} tone="muted" />
              </div>
            </div>
          )}

          {summary.totalDocuments === 0 && (
            <div className="card">
              <EmptyState
                title="هنوز سندی ثبت نشده است."
                description="پس از افزودن اولین سند، آمار اینجا نمایش داده می‌شود."
              />
            </div>
          )}
        </>
      )}
    </div>
  )
}
