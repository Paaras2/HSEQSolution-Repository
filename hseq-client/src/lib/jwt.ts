// Minimal JWT payload decoder. We only ever read claims already verified by
// the backend (signature, issuer, audience, expiry) - the frontend never
// re-validates the token, it just reads claims out of one it already trusts
// because the API accepted it.

export interface JwtClaims {
  sub?: string
  role?: string
  exp?: number
  [claim: string]: unknown
}

function base64UrlDecode(input: string): string {
  const padded = input.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(input.length / 4) * 4, '=')
  const decoded = atob(padded)
  // atob gives us a binary string; re-encode as UTF-8 text.
  const bytes = Uint8Array.from(decoded, (c) => c.charCodeAt(0))
  return new TextDecoder('utf-8').decode(bytes)
}

export function decodeJwt(token: string): JwtClaims | null {
  const parts = token.split('.')
  if (parts.length !== 3) return null
  try {
    return JSON.parse(base64UrlDecode(parts[1])) as JwtClaims
  } catch {
    return null
  }
}

export function isTokenExpired(claims: JwtClaims | null): boolean {
  if (!claims?.exp) return true
  return Date.now() >= claims.exp * 1000
}

// The role claim is written by JwtService using ClaimTypes.Role. System.IdentityModel.Tokens.Jwt
// serializes that as the full claims URI "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
// (verified against an actual issued token) rather than a short "role" key - handle both.
const ROLE_CLAIM_URI = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

export function getRoleClaim(claims: JwtClaims | null): string | null {
  if (!claims) return null
  const value = claims.role ?? claims[ROLE_CLAIM_URI]
  return typeof value === 'string' ? value : null
}

const SUB_CLAIM_URI = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'

export function getSubClaim(claims: JwtClaims | null): string | null {
  if (!claims) return null
  const value = claims.sub ?? claims[SUB_CLAIM_URI]
  return typeof value === 'string' ? value : null
}

// نام و نام خانوادگی را JwtService از پاسخ checkCredential در توکن می‌گذارد
// (given_name / family_name). همان احتیاطِ نقش اینجا هم هست: شکلِ URI هم پذیرفته می‌شود.
const GIVEN_NAME_CLAIM_URI = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'
const SURNAME_CLAIM_URI = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'

function firstText(...values: unknown[]): string | null {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value.trim()
  }
  return null
}

export function getNameClaims(claims: JwtClaims | null): { firstName: string | null; lastName: string | null } {
  if (!claims) return { firstName: null, lastName: null }
  return {
    firstName: firstText(claims.given_name, claims[GIVEN_NAME_CLAIM_URI]),
    lastName: firstText(claims.family_name, claims[SURNAME_CLAIM_URI]),
  }
}
