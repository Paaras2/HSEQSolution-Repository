import { useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { JALALI_MONTH_NAMES, JALALI_WEEKDAY_NAMES, jalaliWeekdayIndex, todayJalali } from '../lib/jalali'
import { applyTheme, resolveTheme, type Theme } from '../lib/theme'
import { toPersianDigits } from '../lib/digits'
import { SearchBar } from './SearchBar'
import {
  AdminIcon,
  CalendarIcon,
  ChevronsIcon,
  DashboardIcon,
  FileTextIcon,
  LogoutIcon,
  MenuIcon,
  MoonIcon,
  SunIcon,
} from './Icons'

const ROLE_LABELS: Record<string, string> = {
  ReadOnly: 'فقط مشاهده',
  DocumentManager: 'مدیر اسناد',
  Admin: 'مدیر سیستم',
}

// حالت جمع‌شده‌ی منو بین نشست‌ها می‌ماند، چون ترجیح فضای کاری کاربر است نه وضعیت لحظه‌ای.
const COLLAPSE_KEY = 'hseq-sidebar-collapsed'

function readCollapsed(): boolean {
  try {
    return window.localStorage.getItem(COLLAPSE_KEY) === '1'
  } catch {
    return false
  }
}

// «شنبه، ۳۱ مرداد ۱۴۰۵» - مثل هر عدد دیگری در رابط کاربری با ارقام فارسی.
function todayLabel(): string {
  const today = todayJalali()
  const now = new Date()
  const weekday = JALALI_WEEKDAY_NAMES[jalaliWeekdayIndex(now.getFullYear(), now.getMonth() + 1, now.getDate())]
  return toPersianDigits(`${weekday}، ${today.jd} ${JALALI_MONTH_NAMES[today.jm - 1]} ${today.jy}`)
}

export function AppShell() {
  const { user, logout, hasCapability } = useAuth()
  // پنل ادمین فقط برای «مدیر اسناد» و «مدیر سیستم» دیده می‌شود.
  const canOpenAdminPanel = hasCapability('admin:access')

  // کشوی موبایل و جمع‌شدنِ ریلِ دسکتاپ دو چیز جدا هستند: اولی موقتی است، دومی ترجیح ماندگار.
  const [isDrawerOpen, setDrawerOpen] = useState(false)
  const [isCollapsed, setCollapsed] = useState(readCollapsed)
  const [theme, setTheme] = useState<Theme>(resolveTheme)

  const dateLabel = useMemo(todayLabel, [])
  const roleLabel = user ? ROLE_LABELS[user.role] : ''

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  useEffect(() => {
    try {
      window.localStorage.setItem(COLLAPSE_KEY, isCollapsed ? '1' : '0')
    } catch {
      /* بی‌اهمیت: فقط ماندگاری ترجیح از دست می‌رود. */
    }
  }, [isCollapsed])

  const navItems = [
    { to: '/documents', end: true, label: 'اسناد', icon: <FileTextIcon size={18} /> },
    { to: '/dashboard', end: true, label: 'داشبورد', icon: <DashboardIcon size={18} /> },
    ...(canOpenAdminPanel
      ? [{ to: '/admin', end: true, label: 'پنل ادمین', icon: <AdminIcon size={18} /> }]
      : []),
  ]

  return (
    <div className={`app-shell${isCollapsed ? ' is-collapsed' : ''}`}>
      {/* پوششِ تیره‌ی پشت کشوی موبایل؛ در دسکتاپ اصلاً رندر نمی‌شود. */}
      {isDrawerOpen && <div className="app-scrim" onClick={() => setDrawerOpen(false)} />}

      <aside className={`app-sidebar${isDrawerOpen ? ' is-open' : ''}`}>
        {/* کارت لوگو. هر دو نسخه رندر می‌شوند و انتخاب بین‌شان با CSS انجام می‌گیرد، نه
            با state: حالت جمع‌شده فقط در دسکتاپ معنا دارد و کشوی موبایل باید همیشه
            لوگوی کامل را نشان دهد. */}
        <div className="sidebar-brand">
          <img
            src="/brand/logo-full.png"
            alt="ODCC - شرکت طراحی و ساختمان نفت"
            className="sidebar-brand__logo sidebar-brand__logo--full"
          />
          <img src="/brand/logo-icon.png" alt="" className="sidebar-brand__logo sidebar-brand__logo--mark" />
        </div>

        <div className="sidebar-title">
          <h1>مدیریت یکپارچه مدارک و مستندات</h1>
          <p>مدیریت هوشمند اسناد کنترل‌شده</p>
        </div>

        {/* عنوان بخش با خطِ کشیده تا انتهای منو */}
        <div className="sidebar-section">
          <span className="sidebar-section__label">ناوبری اصلی</span>
          <span className="sidebar-section__line" aria-hidden="true" />
        </div>

        <nav className="sidebar-nav">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}
              onClick={() => setDrawerOpen(false)}
              // نام دسترس‌پذیر صریح، چون در حالت جمع‌شده متنِ آیتم پنهان می‌شود.
              aria-label={item.label}
            >
              <span className="nav-link__icon">{item.icon}</span>
              <span className="nav-link__text">{item.label}</span>
              {/* در حالت جمع‌شده، متنِ پنهان‌شده به‌صورت راهنمای شناور برمی‌گردد. برای
                  صفحه‌خوان تکراری است، پس از درخت دسترس‌پذیری کنار گذاشته می‌شود. */}
              <span className="nav-link__tip" aria-hidden="true">
                {item.label}
              </span>
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-footer">
          {/* کارت کاربر: هویتِ نشستِ جاری. عملیات خروج در نوار بالا است تا در حالت
              جمع‌شده و در موبایل هم همیشه در دسترس بماند. */}
          <div className="user-card">
            <span className="user-card__avatar">
              {toPersianDigits(user?.pcode?.slice(-2) ?? '--')}
              <span className="user-card__status" aria-hidden="true" />
            </span>
            <span className="user-card__body">
              <strong>کد پرسنلی {toPersianDigits(user?.pcode)}</strong>
              <span>{roleLabel}</span>
            </span>
          </div>

          <button
            type="button"
            className="sidebar-collapse"
            onClick={() => setCollapsed((value) => !value)}
            aria-label={isCollapsed ? 'باز کردن منو' : 'جمع کردن منو'}
          >
            <span className="sidebar-collapse__text">{isCollapsed ? 'باز کردن منو' : 'جمع کردن منو'}</span>
            <ChevronsIcon size={16} />
          </button>
        </div>
      </aside>

      <div className="app-content">
        {/* نگه‌دارنده‌ی چسبان: پس‌زمینه‌اش همان پس‌زمینه‌ی صفحه است تا محتوای در حال
            اسکرول از شکافِ بالای نوار دیده نشود. */}
        <div className="app-topbar-dock">
          <header className="app-topbar">
            <div className="topbar__lead">
              <button
                type="button"
                className="topbar__menu"
                onClick={() => setDrawerOpen((open) => !open)}
                aria-label="باز و بسته کردن منو"
              >
                <MenuIcon size={18} />
              </button>

              <span className="topbar__date">
                <CalendarIcon size={16} />
                {dateLabel}
              </span>

              <button
                type="button"
                className="topbar__icon-btn"
                onClick={() => setTheme((current) => (current === 'dark' ? 'light' : 'dark'))}
                aria-label={theme === 'dark' ? 'نمایش روشن' : 'نمایش تیره'}
                title={theme === 'dark' ? 'نمایش روشن' : 'نمایش تیره'}
              >
                {theme === 'dark' ? <SunIcon size={18} /> : <MoonIcon size={18} />}
              </button>
            </div>

            {/* جستجوی جمع‌وجور - جای صفحه‌ی «جستجوی پیشرفته» را گرفته و فیلترهایش را
                داخل یک پنل شناور دارد. */}
            <SearchBar />

            <div className="topbar__user">
              <span className="topbar__user-info">
                <strong>کد پرسنلی {toPersianDigits(user?.pcode)}</strong>
                <span>{roleLabel}</span>
              </span>
              <span className="topbar__avatar">{toPersianDigits(user?.pcode?.slice(-2) ?? '--')}</span>
              <button type="button" className="topbar__icon-btn" onClick={logout} aria-label="خروج" title="خروج">
                <LogoutIcon size={18} />
              </button>
            </div>
          </header>
        </div>

        <main className="app-main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
