import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { Role } from '../auth/roles'
import { ApiError } from '../lib/httpClient'
import { toLatinDigits, toPersianDigits } from '../lib/digits'
import {
  ChevronDownIcon,
  EyeIcon,
  EyeOffIcon,
  LayersIcon,
  LockIcon,
  SearchIcon,
  SpinnerIcon,
  UserIcon,
  UsersIcon,
} from '../components/Icons'

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

  // نمایش/پنهان‌سازی رمز - فقط حالتِ ظاهری فیلد را عوض می‌کند، نه مقدارش را.
  const [isPasswordVisible, setPasswordVisible] = useState(false)

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
      {/* لایه‌ی پس‌زمینه: سه هاله‌ی نرمِ رنگِ برند که خیلی آرام شناورند. کاملاً تزئینی
          است، پس از درخت دسترس‌پذیری کنار می‌رود و هیچ رویدادی نمی‌گیرد. */}
      <div className="login-aurora" aria-hidden="true">
        <span className="login-aurora__orb login-aurora__orb--1" />
        <span className="login-aurora__orb login-aurora__orb--2" />
        <span className="login-aurora__orb login-aurora__orb--3" />
      </div>

      <div className="login-shell">
        {/* ---- ستون فرم: سمتِ شروع (راست)، چون مسیر خواندن فارسی از همین‌جا آغاز می‌شود ---- */}
        <div className="login-panel">
          <div className="login-panel__head">
            <img src="/brand/logo-icon.png" alt="" className="login-panel__mark" />
            <h1>مدیریت یکپارچه مدارک و مستندات</h1>
            <p>برای ادامه، با کد پرسنلی خود وارد شوید</p>
          </div>

          <form className="login-form" onSubmit={handleSubmit} noValidate>
            {error && (
              <div className="form-error" role="alert">
                {error}
              </div>
            )}

            {/* کد پرسنلی - آیکون داخل فیلد تزئینی است و از درخت دسترس‌پذیری کنار می‌رود */}
            <div className="field">
              <label htmlFor="pcode">کد پرسنلی</label>
              <div className="login-field">
                <span className="login-field__icon" aria-hidden="true">
                  <UserIcon size={17} />
                </span>
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
            </div>

            {/* رمز عبور به‌همراه دکمه‌ی نمایش/پنهان‌سازی */}
            <div className="field">
              <label htmlFor="password">رمز عبور</label>
              <div className="login-field login-field--with-toggle">
                <span className="login-field__icon" aria-hidden="true">
                  <LockIcon size={17} />
                </span>
                <input
                  id="password"
                  name="password"
                  type={isPasswordVisible ? 'text' : 'password'}
                  autoComplete="current-password"
                  className="text-input"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  disabled={isSubmitting}
                />
                <button
                  type="button"
                  className="login-field__toggle"
                  onClick={() => setPasswordVisible((visible) => !visible)}
                  aria-label={isPasswordVisible ? 'پنهان کردن رمز عبور' : 'نمایش رمز عبور'}
                  aria-pressed={isPasswordVisible}
                  disabled={isSubmitting}
                >
                  {isPasswordVisible ? <EyeOffIcon size={17} /> : <EyeIcon size={17} />}
                </button>
              </div>
            </div>

            <button type="submit" className="btn login-submit" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <SpinnerIcon size={17} />
                  در حال ورود...
                </>
              ) : (
                'ورود'
              )}
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
                <ChevronDownIcon size={15} />
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

          {/* امضای سازنده - همان عبارتی که در پای منوی برنامه هم می‌آید */}
          <p className="login-panel__credit">
            <span>طراحی و توسعه، فناوری اطلاعات، ارتباطات و حکمرانی داده</span>
            <span className="login-panel__credit-version">نسخه {toPersianDigits('1.0')}</span>
          </p>
        </div>

        {/* ---- ستون برند: لوگوی تک‌رنگِ سفید روی گرادیانتِ آبی-سبزِ برند ---- */}
        <aside className="login-hero">
          <div className="login-hero__logo" role="img" aria-label="ODCC - شرکت طراحی و ساختمان نفت" />

          <div className="login-hero__text">
            <h2>سامانه‌ی مدیریت مدارک و مستندات</h2>
            <p>شرکت طراحی و ساختمان نفت</p>
          </div>

          {/* سه قابلیت اصلی سامانه - صرفاً معرفی، بدون پیوند */}
          <ul className="login-hero__features">
            <li>
              <span className="login-hero__feature-icon" aria-hidden="true">
                <LayersIcon size={16} />
              </span>
              نسخه‌بندی و بازنگری کنترل‌شده
            </li>
            <li>
              <span className="login-hero__feature-icon" aria-hidden="true">
                <SearchIcon size={16} />
              </span>
              جست‌وجوی پیشرفته در همه‌ی مدارک
            </li>
            <li>
              <span className="login-hero__feature-icon" aria-hidden="true">
                <UsersIcon size={16} />
              </span>
              دسترسی نقش‌محور و کنترل‌شده
            </li>
          </ul>
        </aside>
      </div>
    </div>
  )
}
