import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const ROLE_LABELS: Record<string, string> = {
  ReadOnly: 'فقط مشاهده',
  DocumentManager: 'مدیر اسناد',
  Admin: 'مدیر سیستم',
}

export function AppShell() {
  const { user, logout } = useAuth()
  const [isSidebarOpen, setSidebarOpen] = useState(false)

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__brand">
          <button
            type="button"
            className="btn btn-ghost app-header__menu-toggle"
            onClick={() => setSidebarOpen((open) => !open)}
            aria-label="باز و بسته کردن منو"
          >
            منو
          </button>
          <img src="/brand/logo-icon.png" alt="ODCC" className="app-header__brand-mark" />
          <span>مدیریت اسناد HSEQ</span>
        </div>

        <div className="app-header__user">
          <div className="app-header__user-info">
            <div className="app-header__user-name">کد پرسنلی {user?.pcode}</div>
            <div className="app-header__user-role">{user ? ROLE_LABELS[user.role] : ''}</div>
          </div>
          <button type="button" className="btn btn-secondary btn-sm" onClick={logout}>
            خروج
          </button>
        </div>
      </header>

      <div className="app-body">
        <aside className={`app-sidebar${isSidebarOpen ? ' is-open' : ''}`}>
          <nav className="app-sidebar__nav">
            <NavLink to="/" end className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setSidebarOpen(false)}>
              داشبورد
            </NavLink>
            <NavLink to="/documents" end className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setSidebarOpen(false)}>
              اسناد
            </NavLink>
            <NavLink to="/documents/search" className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => setSidebarOpen(false)}>
              جستجوی پیشرفته
            </NavLink>
          </nav>
        </aside>

        <main className="app-main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}