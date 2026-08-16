import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { authApi } from '../api/authApi'
import { configureHttpClient } from '../lib/httpClient'
import { decodeJwt, getRoleClaim, getSubClaim, isTokenExpired } from '../lib/jwt'
import { capabilitiesFor, resolveRole } from './roles'
import type { Capability, Role } from './roles'

const TOKEN_STORAGE_KEY = 'hseq.auth.token'

interface AuthUser {
  pcode: string
  role: Role
}

interface AuthContextValue {
  isAuthenticated: boolean
  isInitializing: boolean
  user: AuthUser | null
  login: (pcode: string, password: string) => Promise<void>
  devLogin: (role: Role) => Promise<void>
  logout: () => void
  hasCapability: (capability: Capability) => boolean
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

  // The http client needs to read the *current* token from outside React's
  // render cycle - a ref avoids re-subscribing it on every token change.
  const tokenRef = useRef<string | null>(null)
  tokenRef.current = token

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_STORAGE_KEY)
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

  const login = useCallback(async (pcode: string, password: string) => {
    const result = await authApi.login(pcode, password)
    localStorage.setItem(TOKEN_STORAGE_KEY, result.token)
    setToken(result.token)
  }, [])

  const devLogin = useCallback(async (role: Role) => {
    const result = await authApi.devLogin(role)
    localStorage.setItem(TOKEN_STORAGE_KEY, result.token)
    setToken(result.token)
  }, [])

  const user = useMemo<AuthUser | null>(() => {
    if (!token) return null
    const claims = decodeJwt(token)
    const pcode = getSubClaim(claims)
    if (!pcode) return null
    return { pcode, role: resolveRole(getRoleClaim(claims)) }
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
    }),
    [user, isInitializing, login, devLogin, logout, capabilities],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}