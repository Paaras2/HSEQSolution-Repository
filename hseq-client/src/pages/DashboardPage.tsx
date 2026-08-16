import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { documentApi } from '../api/documentApi'

export function DashboardPage() {
  const { user, hasCapability } = useAuth()
  const [activeCount, setActiveCount] = useState<number | null>(null)

  useEffect(() => {
    let cancelled = false
    documentApi
      .getPaged(1, 1, false)
      .then((result) => {
        if (!cancelled) setActiveCount(result.totalCount)
      })
      .catch(() => {
        if (!cancelled) setActiveCount(null)
      })
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>داشبورد</h1>
          <p>خوش آمدید، کد پرسنلی {user?.pcode}.</p>
        </div>
      </div>

      <div className="card" style={{ padding: 20, maxWidth: 320 }}>
        <div className="text-muted" style={{ fontSize: 13, marginBottom: 6 }}>
          اسناد فعال
        </div>
        <div style={{ fontSize: 28, fontWeight: 700 }}>{activeCount ?? '—'}</div>
        <Link to="/documents" className="btn btn-secondary btn-sm" style={{ marginTop: 16 }}>
          {hasCapability('documents:manage') ? 'مدیریت اسناد' : 'مشاهده اسناد'}
        </Link>
      </div>
    </div>
  )
}