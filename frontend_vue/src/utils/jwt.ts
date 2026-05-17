// ============================================================
// JWT utility functions
// All decoding is done client-side for UX purposes only.
// The server ALWAYS validates the token — never trust client-side decode for security.
// ============================================================

import type { JwtClaims } from '@/types/auth'

// ASP.NET Core Identity full-URI claim types (used when outbound mapping is NOT applied)'
//standard jwt vs URI claim types
const CLAIM_LONG = {
  sub: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  email: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
  given_name: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname',
  family_name: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname',
  role: 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
} as const

/**
 * Decode the payload of a JWT without verifying the signature.
 * Returns null if the token is malformed.
 *
 * Normalises ASP.NET Core Identity claims so that both short JWT names
 * (`sub`, `email`, `role`, …) and full-URI claim types are resolved to
 * the short names expected by the rest of the frontend code.
 */
export function decodeJwt(token: string): JwtClaims | null {
  try {
    const parts = token.split('.')
    if (parts.length !== 3) return null

    // Base64url decode the payload
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join(''),
    )
    const raw = JSON.parse(jsonPayload) as Record<string, unknown>

    // Normalise: if a short-name key is missing, try the full-URI key
    for (const [short, long] of Object.entries(CLAIM_LONG)) {
      if (raw[short] === undefined && raw[long] !== undefined) {
        raw[short] = raw[long]
      }
    }

    return raw as unknown as JwtClaims
  } catch {
    return null
  }
}

/**
 * Returns true if the token's exp claim is in the future
 * with an optional buffer (default 30 seconds to account for clock drift).
 */
export function isTokenValid(token: string | null, bufferSeconds = 30): boolean {
  if (!token) return false
  const claims = decodeJwt(token)
  if (!claims?.exp) return false
  const nowSeconds = Math.floor(Date.now() / 1000)
  return claims.exp > nowSeconds + bufferSeconds
}

/**
 * Extract the user's roles from the JWT claims.
 * ASP.NET Core may emit roles as a single string or array,
 * under the short name `role` or the full URI claim type.
 */
export function getRolesFromToken(token: string): string[] {
  const claims = decodeJwt(token)
  if (!claims) return []
  const role = claims.role
  if (!role) return []
  return Array.isArray(role) ? role : [role]
}

/**
 * How many seconds until the token expires (negative = already expired).
 */
export function secondsUntilExpiry(token: string): number {
  const claims = decodeJwt(token)
  if (!claims?.exp) return -1
  return claims.exp - Math.floor(Date.now() / 1000)
}
