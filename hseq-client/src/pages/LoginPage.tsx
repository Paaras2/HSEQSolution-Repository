import { useEffect, useRef, useState } from 'react'
import type { FormEvent, KeyboardEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { Role } from '../auth/roles'
import {
  SLOW_LOGIN_HINT_MS,
  describeLoginFailure,
  looksLikePersianKeyboard,
  normalizeUsername,
  validateLogin,
} from '../auth/loginFeedback'
import type { LoginFailure, LoginFieldErrors } from '../auth/loginFeedback'
import { ApiError } from '../lib/httpClient'
import { toPersianDigits } from '../lib/digits'
import { releaseLabel } from '../lib/release'
import {
  AlertTriangleIcon,
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

// ورود با کد پرسنلی و رمزِ سامانه‌ی مدیریت کاربران (checkCredential).
//
// هر پاسخِ ناموفق یکی از چند علتِ مشخص دارد و هر کدام پیام و کارِ متفاوتی می‌خواهد:
// رمز غلط (رمز پاک و فیلدش فوکوس می‌شود)، حساب غیرفعال، و در دسترس نبودنِ سامانه‌ی
// کاربران (هشدارِ کهربایی با «تلاش دوباره» - تا کاربر رمزِ درستش را بی‌دلیل عوض نکند).
// ترجمه‌ی پاسخ به پیام در loginFeedback.ts است و بدون مرورگر آزموده می‌شود.
export function LoginPage() {
  const { isAuthenticated, login, devLogin } = useAuth()
  const navigate = useNavigate()

  const [pcode, setPcode] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<LoginFieldErrors>({})
  const [failure, setFailure] = useState<LoginFailure | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  // پاسخ کُند: سرور تا ۱۰ ثانیه منتظر سامانه‌ی کاربران می‌ماند. بدون این پیام، دکمه‌ی
  // چرخان پس از چند ثانیه شبیهِ صفحه‌ی قفل‌شده است و کاربر پشت سر هم کلیک می‌کند.
  const [isSlow, setIsSlow] = useState(false)

  // نمایش/پنهان‌سازی رمز - فقط حالتِ ظاهری فیلد را عوض می‌کند، نه مقدارش را.
  const [isPasswordVisible, setPasswordVisible] = useState(false)
  const [isCapsLockOn, setCapsLockOn] = useState(false)

  const [isDevOpen, setIsDevOpen] = useState(false)
  const [devRole, setDevRole] = useState<Role | ''>('')
  const [isDevSubmitting, setIsDevSubmitting] = useState(false)
  const [devError, setDevError] = useState<string | null>(null)

  const pcodeRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)

  // فیلدها هنگام ارسال غیرفعال‌اند و فیلدِ غیرفعال فوکوس نمی‌گیرد؛ پس فوکوسِ پس از خطا
  // تا رندرِ بعدی که دوباره فعال شده‌اند صبر می‌کند.
  const pendingFocus = useRef<'pcode' | 'password' | null>(null)
  useEffect(() => {
    if (isSubmitting || !pendingFocus.current) return
    const target = pendingFocus.current === 'pcode' ? pcodeRef : passwordRef
    pendingFocus.current = null
    target.current?.focus()
  }, [isSubmitting])

  if (isAuthenticated) {
    return <Navigate to={AFTER_LOGIN_PATH} replace />
  }

  const isPersianKeyboard = looksLikePersianKeyboard(password)
  const passwordHintIds = [
    fieldErrors.password ? 'password-error' : null,
    isCapsLockOn || isPersianKeyboard ? 'password-hints' : null,
  ]
    .filter(Boolean)
    .join(' ')

  async function submit() {
    // Duplicate-submission protection: ignore re-entrant submits (double
    // click, double Enter, retry while in flight).
    if (isSubmitting) return

    const username = normalizeUsername(pcode)
    const errors = validateLogin(username, password)
    setFieldErrors(errors)
    if (errors.username || errors.password) {
      setFailure(null)
      ;(errors.username ? pcodeRef : passwordRef).current?.focus()
      return
    }

    setIsSubmitting(true)
    setFailure(null)
    const slowTimer = window.setTimeout(() => setIsSlow(true), SLOW_LOGIN_HINT_MS)

    try {
      await login(username, password)
      navigate(AFTER_LOGIN_PATH, { replace: true })
    } catch (err) {
      const described = describeLoginFailure(err)
      setFailure(described)
      if (described.kind === 'invalid_credentials') {
        // رمزِ غلط پاک می‌شود تا کاربر از نو تایپش کند؛ کد پرسنلی می‌ماند.
        setPassword('')
        pendingFocus.current = 'password'
      }
    } finally {
      window.clearTimeout(slowTimer)
      setIsSlow(false)
      setIsSubmitting(false)
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    void submit()
  }

  // خطای «رمز غلط» با شروعِ تایپ دوباره کهنه می‌شود؛ هشدارِ در دسترس نبودنِ سامانه نه،
  // چون ربطی به آنچه کاربر تایپ می‌کند ندارد و دکمه‌ی تلاش دوباره‌اش باید بماند.
  function clearStaleFailure() {
    if (failure && !failure.canRetry) setFailure(null)
  }

  function trackCapsLock(event: KeyboardEvent<HTMLInputElement>) {
    setCapsLockOn(event.getModifierState('CapsLock'))
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
            <p>با کد پرسنلی و رمز عبورِ سامانه‌ی مدیریت کاربران وارد شوید</p>
          </div>

          <form className="login-form" onSubmit={handleSubmit} noValidate aria-busy={isSubmitting}>
            {failure && (
              <div className={`login-alert login-alert--${failure.tone}`} role="alert">
                <span className="login-alert__icon" aria-hidden="true">
                  <AlertTriangleIcon size={18} />
                </span>
                <div className="login-alert__body">
                  <strong>{failure.title}</strong>
                  {failure.detail && <p>{failure.detail}</p>}
                  {failure.canRetry && (
                    <button
                      type="button"
                      className="login-alert__retry"
                      onClick={() => void submit()}
                      disabled={isSubmitting}
                    >
                      تلاش دوباره
                    </button>
                  )}
                </div>
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
                  ref={pcodeRef}
                  id="pcode"
                  name="username"
                  type="text"
                  inputMode="numeric"
                  autoComplete="username"
                  autoCapitalize="off"
                  spellCheck={false}
                  maxLength={32}
                  className="text-input"
                  value={pcode}
                  onChange={(event) => {
                    setPcode(event.target.value)
                    if (fieldErrors.username) setFieldErrors((errors) => ({ ...errors, username: undefined }))
                    clearStaleFailure()
                  }}
                  aria-invalid={fieldErrors.username ? true : undefined}
                  aria-describedby={fieldErrors.username ? 'pcode-error' : undefined}
                  disabled={isSubmitting}
                  autoFocus
                />
              </div>
              {fieldErrors.username && (
                <p id="pcode-error" className="login-field-error">
                  {fieldErrors.username}
                </p>
              )}
            </div>

            {/* رمز عبور به‌همراه دکمه‌ی نمایش/پنهان‌سازی */}
            <div className="field">
              <label htmlFor="password">رمز عبور</label>
              <div className="login-field login-field--with-toggle">
                <span className="login-field__icon" aria-hidden="true">
                  <LockIcon size={17} />
                </span>
                <input
                  ref={passwordRef}
                  id="password"
                  name="password"
                  type={isPasswordVisible ? 'text' : 'password'}
                  autoComplete="current-password"
                  autoCapitalize="off"
                  spellCheck={false}
                  className="text-input"
                  value={password}
                  onChange={(event) => {
                    setPassword(event.target.value)
                    if (fieldErrors.password) setFieldErrors((errors) => ({ ...errors, password: undefined }))
                    clearStaleFailure()
                  }}
                  onKeyDown={trackCapsLock}
                  onKeyUp={trackCapsLock}
                  aria-invalid={fieldErrors.password ? true : undefined}
                  aria-describedby={passwordHintIds || undefined}
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
              {fieldErrors.password && (
                <p id="password-error" className="login-field-error">
                  {fieldErrors.password}
                </p>
              )}
              {(isCapsLockOn || isPersianKeyboard) && (
                <ul id="password-hints" className="login-field-hints" aria-live="polite">
                  {isCapsLockOn && <li>Caps Lock روشن است.</li>}
                  {isPersianKeyboard && <li>صفحه‌کلید روی فارسی است؛ رمزها معمولاً با حروف و ارقام لاتین‌اند.</li>}
                </ul>
              )}
            </div>

            <button type="submit" className="btn login-submit" disabled={isSubmitting}>
              {isSubmitting ? (
                <>
                  <SpinnerIcon size={17} />
                  در حال بررسی...
                </>
              ) : (
                'ورود'
              )}
            </button>

            {isSlow && (
              <p className="login-slow" role="status">
                پاسخ سامانه‌ی مدیریت کاربران کمی طول کشیده؛ لطفاً صبر کنید.
              </p>
            )}

            <p className="login-help">
              همان رمزی که در سامانه‌ی مدیریت کاربران دارید. برای بازیابی رمز عبور با واحد فناوری اطلاعات تماس بگیرید.
            </p>
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
            <span className="login-panel__credit-version">
              نسخه {toPersianDigits('1.0')}
              {releaseLabel && (
                <>
                  {' · ساخت '}
                  <bdi>{releaseLabel}</bdi>
                </>
              )}
            </span>
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
              ورود یکپارچه با حساب سامانه‌ی مدیریت کاربران
            </li>
          </ul>
        </aside>
      </div>
    </div>
  )
}
