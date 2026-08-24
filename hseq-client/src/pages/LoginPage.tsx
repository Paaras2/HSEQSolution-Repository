import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { Role } from '../auth/roles'
import { ApiError } from '../lib/httpClient'
import { toLatinDigits } from '../lib/digits'

// مقصد بعد از ورود - همیشه فهرست اسناد، چون کار روزمره‌ی کاربر همان‌جاست.
//
// عمداً به «صفحه‌ی قبلی» برنمی‌گردیم: ProtectedRoute هنگام هدایت به صفحه‌ی ورود،
// صفحه‌ی جاری را در state.from می‌گذارد و این شامل حالت «خروج عمدی» هم می‌شد؛
// نتیجه‌اش این بود که اگر کاربر روی داشبورد خروج می‌زد، ورود بعدی دوباره به داشبورد
// می‌رفت نه به اسناد.
const AFTER_LOGIN_PATH = '/documents'

const DEV_ROLE_OPTIONS: { value: Role; label: string }[] = [
  { value: 'ReadOnly', label: 'فقط مشاهده' },
  { value: 'DocumentManager', label: 'مدیر اسناد' },
  { value: 'Admin', label: 'مدیر سیستم' },
]

export function LoginPage() {
  const { isAuthenticated, login, devLogin } = useAuth()
  const navigate = useNavigate()

  const [pcode, setPcode] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [isDevOpen, setIsDevOpen] = useState(false)
  const [devRole, setDevRole] = useState<Role | ''>('')
  const [isDevSubmitting, setIsDevSubmitting] = useState(false)
  const [devError, setDevError] = useState<string | null>(null)

  if (isAuthenticated) {
    return <Navigate to={AFTER_LOGIN_PATH} replace />
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    // Duplicate-submission protection: ignore re-entrant submits (double
    // click, double Enter) while a login request is already in flight.
    if (isSubmitting) return

    // کاربر ممکن است با صفحه‌کلید فارسی عدد بزند؛ سرور فقط ارقام لاتین می‌پذیرد.
    const trimmedPcode = toLatinDigits(pcode).trim()
    if (!trimmedPcode || !password) {
      setError('لطفاً کد پرسنلی و رمز عبور را وارد کنید.')
      return
    }

    setIsSubmitting(true)
    setError(null)
    try {
      await login(trimmedPcode, password)
      navigate(AFTER_LOGIN_PATH, { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'امکان ورود وجود ندارد. لطفاً دوباره تلاش کنید.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleDevLogin() {
    if (isDevSubmitting || !devRole) return

    setIsDevSubmitting(true)
    setDevError(null)
    try {
      await devLogin(devRole)
      navigate(AFTER_LOGIN_PATH, { replace: true })
    } catch (err) {
      setDevError(err instanceof ApiError ? err.message : 'امکان ورود وجود ندارد. لطفاً دوباره تلاش کنید.')
    } finally {
      setIsDevSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-card__brand">
          <img src="/brand/logo-icon.png" alt="ODCC" className="login-card__brand-mark" />
          <h1>مدیریت یکپارچه مدارک و مستندات</h1>
          <p>برای ادامه، با کد پرسنلی خود وارد شوید</p>
        </div>

        <form className="login-form" onSubmit={handleSubmit} noValidate>
          {error && (
            <div className="form-error" role="alert">
              {error}
            </div>
          )}

          <div className="field">
            <label htmlFor="pcode">کد پرسنلی</label>
            <input
              id="pcode"
              name="pcode"
              type="text"
              inputMode="numeric"
              autoComplete="username"
              className="text-input"
              value={pcode}
              onChange={(event) => setPcode(event.target.value)}
              disabled={isSubmitting}
              autoFocus
            />
          </div>

          <div className="field">
            <label htmlFor="password">رمز عبور</label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              className="text-input"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              disabled={isSubmitting}
            />
          </div>

          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting ? 'در حال ورود...' : 'ورود'}
          </button>
        </form>

        {/* Dev-only sign-in shortcut - see AuthController.DevLogin. Never rendered in
            a production build, since import.meta.env.DEV is compiled out by Vite. */}
        {import.meta.env.DEV && (
          <div className="dev-login">
            <button
              type="button"
              className="dev-login__toggle"
              onClick={() => setIsDevOpen((open) => !open)}
              aria-expanded={isDevOpen}
            >
              <span>{isDevOpen ? '\u2039' : '\u203A'}</span>
              ورود توسعه‌دهنده
            </button>

            {isDevOpen && (
              <div className="dev-login__body">
                <p className="dev-login__hint">
                  فقط محیط توسعه - حساب‌های آزمایشی، داده‌های غیرماندگار.
                </p>

                {devError && (
                  <div className="form-error" role="alert">
                    {devError}
                  </div>
                )}

                <div className="field">
                  <label htmlFor="dev-role">نقش آزمایشی</label>
                  <select
                    id="dev-role"
                    className="select-input"
                    value={devRole}
                    onChange={(event) => setDevRole(event.target.value as Role)}
                    disabled={isDevSubmitting}
                  >
                    <option value="">یک نقش آزمایشی را انتخاب کنید...</option>
                    {DEV_ROLE_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>

                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={handleDevLogin}
                  disabled={isDevSubmitting || !devRole}
                >
                  {isDevSubmitting ? 'در حال ورود...' : 'ورود مستقیم'}
                </button>

                <p className="dev-login__hint">
                  این حساب‌ها ساختگی هستند، فقط برای آزمودن نقش‌های رابط کاربری استفاده می‌شوند و در نسخه‌ی عملیاتی وجود ندارند.
                </p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}