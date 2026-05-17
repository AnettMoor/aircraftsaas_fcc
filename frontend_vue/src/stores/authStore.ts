// ============================================================
// Pinia Auth Store
//
// Responsibilities:
// - Hold access token in memory for runtime use
// - Persist both JWT and refresh token to localStorage via storage utility
// - login / logout / register / silentRefresh
// - Register itself with the Axios client's token provider
// - On cold start, restore session from localStorage tokens
// ============================================================

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { setTokenProvider, setTenantProvider, setTenantRefresher } from '@/api/client'
import { storage } from '@/utils/storage'
import { isTokenValid } from '@/utils/jwt'
import type { LoginInfo, RegisterInfo, JWTResponse } from '@/types/auth'
import { useSessionStore } from './sessionStore'

export const useAuthStore = defineStore('auth', () => {
  // ----------------------------------------------------------------
  // State
  // ----------------------------------------------------------------
  /** Access token — kept in memory for Axios interceptor, also persisted to localStorage */
  const accessToken = ref<string | null>(null)
  /** Whether a token refresh is in progress */
  const isRefreshing = ref(false)
  /** Whether the initial session restore has completed */
  const sessionInitialized = ref(false)

  // ----------------------------------------------------------------
  // Getters
  // ----------------------------------------------------------------
  const isAuthenticated = computed(() => {
    return isTokenValid(accessToken.value)
  })

  const hasRefreshToken = computed(() => {
    return !!storage.getRefreshToken()
  })

  // ----------------------------------------------------------------
  // Token management (called by Axios interceptor via window)
  // ----------------------------------------------------------------
  function setTokens(jwt: string, refreshToken: string): void {
    accessToken.value = jwt
    storage.setJwt(jwt)
    storage.setRefreshToken(refreshToken)

    // Update session store from new JWT claims
    const sessionStore = useSessionStore()
    sessionStore.hydrateFromJwt(jwt)
  }

  /**
   * Fetch the user's company profile from the API and populate the session store.
   * Called after login, register, and session restore so the frontend knows which
   * company the user belongs to (populates activeCompany, enables X-Tenant-Id header).
   * Returns true if the active company changed (useful for stale-tenant retry logic).
   */
  async function fetchUserProfile(): Promise<boolean> {
    try {
      const { userService } = await import('@/api/services/userService')
      const companies = await userService.getMyCompanies()
      const sessionStore = useSessionStore()
      const previousCompanyId = sessionStore.activeCompany?.companyId ?? null
      if (sessionStore.user) {
        sessionStore.setUserProfile({
          ...sessionStore.user,
          companies,
        })
      }
      const newCompanyId = sessionStore.activeCompany?.companyId ?? null
      return previousCompanyId !== null && newCompanyId !== null && previousCompanyId !== newCompanyId
    } catch {
      // Profile fetch is best-effort; don't block auth flow
      // (e.g. newly registered user may not have a company yet)
      return false
    }
  }

  function clearAuth(): void {
    accessToken.value = null
    storage.clearJwt()
    storage.clearRefreshToken()

    const sessionStore = useSessionStore()
    sessionStore.clearSession()
  }

  // ----------------------------------------------------------------
  // Auth actions
  // ----------------------------------------------------------------
  async function login(credentials: LoginInfo): Promise<void> {
    // Import here to avoid circular dependency
    const { authService } = await import('@/api/services/authService')
    const response: JWTResponse = await authService.login(credentials)
    setTokens(response.jwt, response.refreshToken)
    await fetchUserProfile()
  }

  async function register(info: RegisterInfo): Promise<void> {
    const { authService } = await import('@/api/services/authService')
    const response: JWTResponse = await authService.register(info)
    setTokens(response.jwt, response.refreshToken)
    await fetchUserProfile()
  }

  async function logout(): Promise<void> {
    const refreshToken = storage.getRefreshToken()
    if (refreshToken && accessToken.value) {
      try {
        const { authService } = await import('@/api/services/authService')
        await authService.logout({ refreshToken })
      } catch {
        // Proceed with local logout even if server call fails
      }
    }
    clearAuth()
  }

  /**
   * Silently attempt to restore the session from stored tokens.
   * Strategy:
   * 1. If a valid (non-expired) JWT is in localStorage, restore it directly — no API call needed.
   * 2. If the JWT is expired but we have a refresh token, call the backend to get new tokens.
   * 3. If no tokens are stored, the user must log in.
   * Returns true if session was successfully restored.
   */
  async function silentRefresh(): Promise<boolean> {
    // If we already have a valid access token in memory, ensure profile is loaded
    if (isTokenValid(accessToken.value)) {
      const sessionStore = useSessionStore()
      if (!sessionStore.activeCompany) {
        await fetchUserProfile()
      }
      sessionInitialized.value = true
      return true
    }

    const storedJwt = storage.getJwt()
    const refreshToken = storage.getRefreshToken()

    // Step 1: Try to restore from a still-valid JWT in localStorage
    if (storedJwt && isTokenValid(storedJwt)) {
      accessToken.value = storedJwt
      // Hydrate session store from the restored JWT
      const sessionStore = useSessionStore()
      sessionStore.hydrateFromJwt(storedJwt)
      // Fetch company profile so activeCompany is populated
      await fetchUserProfile()
      sessionInitialized.value = true
      return true
    }

    // Step 2: JWT is expired or missing, but we have a refresh token + expired JWT → refresh
    if (storedJwt && refreshToken) {
      isRefreshing.value = true
      try {
        const { authService } = await import('@/api/services/authService')
        const response = await authService.refreshToken({
          jwt: storedJwt,
          refreshToken,
        })
        setTokens(response.jwt, response.refreshToken)
        await fetchUserProfile()
        sessionInitialized.value = true
        return true
      } catch {
        // Refresh failed — clear everything, user must re-login
        clearAuth()
        sessionInitialized.value = true
        return false
      } finally {
        isRefreshing.value = false
      }
    }

    // Step 3: No stored tokens — user must log in
    sessionInitialized.value = true
    return false
  }

  // ----------------------------------------------------------------
  // Bootstrap: register with Axios client
  // ----------------------------------------------------------------
  function bootstrap(): void {
    // Register token provider with Axios client
    setTokenProvider(
      () => accessToken.value,
      () => {
        clearAuth()
        // Redirect to login — using router import to avoid circular dep
        import('@/router').then(({ router }) => {
          router.push({ name: 'login' })
        })
      },
    )

    // Register tenant provider so every API request includes X-Tenant-Id
    const sessionStore = useSessionStore()
    setTenantProvider(() => sessionStore.activeCompany?.companyId ?? null)

    // Register tenant refresher — called by Axios 403 interceptor when
    // the current X-Tenant-Id is stale (e.g. after SystemAdmin company change)
    setTenantRefresher(async () => fetchUserProfile())

    // Register with window for Axios interceptor token updates
    window.__authStore__ = {
      setTokens: (jwt: string, rt: string) => {
        accessToken.value = jwt
        storage.setJwt(jwt)
        storage.setRefreshToken(rt)
        const sessionStore = useSessionStore()
        sessionStore.hydrateFromJwt(jwt)
      },
    }
  }

  return {
    // state
    accessToken,
    isRefreshing,
    sessionInitialized,
    // getters
    isAuthenticated,
    hasRefreshToken,
    // actions
    setTokens,
    clearAuth,
    login,
    register,
    logout,
    silentRefresh,
    bootstrap,
  }
})
