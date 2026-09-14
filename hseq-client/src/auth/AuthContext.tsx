import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { authApi } from '../api/authApi'
import { configureHttpClient } from '../lib/httpClient'
import { decodeJwt, getNameClaims, getRoleClaim, getSubClaim, isTokenExpired } from '../lib/jwt'
import { displayNameOf, initialsOf } from '../lib/personName'
import type { LoginUser } from '../types/api'
import { capabilitiesFor, resolveRole } from './roles'
import type { Capability, Role } from './roles'

const TOKEN_STORAGE_KEY = 'hseq.auth.token'

// «هنوز با رمز پیش‌فرض وارد می‌شوید.» در sessionStorage است، نه در state: با ری‌لود
// صفحه نباید از بین برود. و نه در localStorage: اگر کاربر تا فردا رمزش را در سامانه‌ی
// مدیریت کاربران عوض کند، پیام نباید دوباره ظاهر شود - فقط ورودِ بعدی می‌گوید
// هنوز پیش‌فرض است یا نه.
const FIRST_LOGIN_NOTICE_KEY = 'hseq.auth.firstLoginNotice'

function readNotice(): boolean {
  try {
    return window.sessionStorage.getItem(FIRST_LOGIN_NOTICE_KEY) === '1'
  } catch {
    return false
  }
}

function writeNotice(visible: boolean): void {
  try {
    if (visible) window.sessionStorage.setItem(FIRST_LOGIN_NOTICE_KEY, '1')
    else window.sessionStorage.removeItem(FIRST_LOGIN_NOTICE_KEY)
  } catch {
    /* بی‌اهمیت: فقط پیام پس از ری‌لود نمی‌ماند. */
  }
}

interface AuthUser {
  pcode: string
  role: Role
  firstName: string | null
  lastName: string | null
  /** «نام نام‌خانوادگی» از سامانه‌ی مدیریت کاربران، یا null اگر توکن نامی ندارد. */
  displayName: string | null
  /** دو حرفِ آواتار، یا null اگر نامی نیست. */
  initials: string | null
}

interface AuthContextValue {
  isAuthenticated: boolean
  isInitializing: boolean
  user: AuthUser | null
  login: (username: string, password: string) => Promise<LoginUser | undefined>
  devLogin: (role: Role) => Promise<void>
  logout: () => void
  hasCapability: (capability: Capability) => boolean
  showFirstLoginNotice: boolean
  dismissFirstLoginNotice: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function readStoredToken(): string | null {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY)
  if (!token) return null
  if (isTokenExpired(decodeJwt(token))) {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
    return null
  }
  return token
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const [showFirstLoginNotice, setShowFirstLoginNotice] = useState(readNotice)

  // The http client needs to read the *current* token from outside React's
  // render cycle - a ref avoids re-subscribing it on every token change.
  const tokenRef = useRef<string | null>(null)
  tokenRef.current = token

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
    writeNotice(false)
    setShowFirstLoginNotice(false)
    setToken(null)
  }, [])

  useEffect(() => {
    // A 401 from any API call means the token is missing/invalid/expired as
    // far as the backend is concerned - treat it as an implicit logout.
    configureHttpClient(() => tokenRef.current, logout)
  }, [logout])

  useEffect(() => {
    setToken(readStoredToken())
    setIsInitializing(false)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
    const result = await authApi.login(username, password)
    const isFirstLogin = result.user?.isFirstLogin === true
    writeNotice(isFirstLogin)
    setShowFirstLoginNotice(isFirstLogin)
    localStorage.setItem(TOKEN_STORAGE_KEY, result.token)
    setToken(result.token)
    return result.user
  }, [])

  const devLogin = useCallback(async (role: Role) => {
    const result = await authApi.devLogin(role)
    localStorage.setItem(TOKEN_STORAGE_KEY, result.token)
    setToken(result.token)
  }, [])

  const dismissFirstLoginNotice = useCallback(() => {
    writeNotice(false)
    setShowFirstLoginNotice(false)
  }, [])

  const user = useMemo<AuthUser | null>(() => {
    if (!token) return null
    const claims = decodeJwt(token)
    const pcode = getSubClaim(claims)
    if (!pcode) return null
    const { firstName, lastName } = getNameClaims(claims)
    return {
      pcode,
      role: resolveRole(getRoleClaim(claims)),
      firstName,
      lastName,
      displayName: displayNameOf({ firstName, lastName }),
      initials: initialsOf({ firstName, lastName }),
    }
  }, [token])

  const capabilities = useMemo(() => capabilitiesFor(user?.role ?? 'ReadOnly'), [user])

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated: user !== null,
      isInitializing,
      user,
      login,
      devLogin,
      logout,
      hasCapability: (capability: Capability) => capabilities.has(capability),
      showFirstLoginNotice: user !== null && showFirstLoginNotice,
      dismissFirstLoginNotice,
    }),
    [user, isInitializing, login, devLogin, logout, capabilities, showFirstLoginNotice, dismissFirstLoginNotice],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
