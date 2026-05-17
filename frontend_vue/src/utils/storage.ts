// ============================================================
// Typed localStorage wrappers
// Both the JWT and refresh token are persisted to localStorage
// so sessions survive page refresh.
// ============================================================

const REFRESH_TOKEN_KEY = 'aircraft_saas_rt'
const JWT_KEY = 'aircraft_saas_jwt'
const LOCALE_KEY = 'aircraft_saas_locale'

export const storage = {
  /** Persist the refresh token to localStorage */
  setRefreshToken(token: string): void {
    try {
      localStorage.setItem(REFRESH_TOKEN_KEY, token)
    } catch {
      // Silently fail (private browsing may block localStorage)
    }
  },

  /** Retrieve the refresh token from localStorage */
  getRefreshToken(): string | null {
    try {
      return localStorage.getItem(REFRESH_TOKEN_KEY)
    } catch {
      return null
    }
  },

  /** Remove the refresh token from localStorage */
  clearRefreshToken(): void {
    try {
      localStorage.removeItem(REFRESH_TOKEN_KEY)
    } catch {
      // ignore
    }
  },

  /** Persist the JWT to localStorage (used as hint for silent refresh on reload) */
  setJwt(token: string): void {
    try {
      localStorage.setItem(JWT_KEY, token)
    } catch {
      // Silently fail
    }
  },

  /** Retrieve the stored JWT from localStorage */
  getJwt(): string | null {
    try {
      return localStorage.getItem(JWT_KEY)
    } catch {
      return null
    }
  },

  /** Remove the JWT from localStorage */
  clearJwt(): void {
    try {
      localStorage.removeItem(JWT_KEY)
    } catch {
      // ignore
    }
  },

  /** Persist user's preferred locale */
  setLocale(locale: string): void {
    try {
      localStorage.setItem(LOCALE_KEY, locale)
    } catch {
      // ignore
    }
  },

  /** Get user's preferred locale */
  getLocale(): string | null {
    try {
      return localStorage.getItem(LOCALE_KEY)
    } catch {
      return null
    }
  },
}
