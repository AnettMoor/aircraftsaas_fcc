// ============================================================
// Axios HTTP Client
//
// Security strategy:
// - Access token (JWT) injected from in-memory Pinia store only
// - Refresh token read from localStorage on 401
// - isRefreshing flag + failedQueue prevent infinite refresh loops
// - The /refreshtokendata endpoint is excluded from 401 retry logic
// ============================================================

import axios, { type AxiosError, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'
import { type JWTResponse, type TokenRefreshInfo } from '@/types/auth'
import { ApiError } from '@/types/api'
import { storage } from '@/utils/storage'

const API_VERSION = import.meta.env.VITE_API_VERSION ?? '1'
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5219'

export const API_BASE = `${API_BASE_URL}/api/v${API_VERSION}`

// ----------------------------------------------------------------
// Pending request queue for when a refresh is in progress
// ----------------------------------------------------------------
interface QueueItem {
  resolve: (value: string) => void
  reject: (reason?: unknown) => void
}

let isRefreshing = false
const failedQueue: QueueItem[] = []

// Track 403 tenant-refresh state to avoid concurrent retries
let isTenantRefreshing = false
let onTenantRefreshed: (() => void) | null = null

function processQueue(error: unknown, token: string | null = null): void {
  failedQueue.forEach(item => {
    if (error) {
      item.reject(error)
    } else if (token) {
      item.resolve(token)
    }
  })
  failedQueue.length = 0
}

// ----------------------------------------------------------------
// Token provider — set by authStore to avoid circular dependency
// ----------------------------------------------------------------
let getAccessToken: (() => string | null) | null = null
let onAuthFailure: (() => void) | null = null
let getTenantId: (() => string | null) | null = null
let refreshTenantContext: (() => Promise<boolean>) | null = null

export function setTokenProvider(
  provider: () => string | null,
  failureCallback: () => void,
): void {
  getAccessToken = provider
  onAuthFailure = failureCallback
}

/**
 * Register a callback that returns the active company/tenant ID.
 * Called by authStore.bootstrap() to avoid circular imports.
 */
export function setTenantProvider(provider: () => string | null): void {
  getTenantId = provider
}

/**
 * Register a callback that re-fetches the user's company context from the API.
 * Returns true if the tenant context changed (and the caller should retry).
 */
export function setTenantRefresher(refresher: () => Promise<boolean>): void {
  refreshTenantContext = refresher
}

// ----------------------------------------------------------------
// Axios instance
// ----------------------------------------------------------------
export const apiClient = axios.create({
  baseURL: API_BASE,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
  },
  timeout: 30000,
})

// ----------------------------------------------------------------
// Request interceptor — inject Bearer token + tenant context
// ----------------------------------------------------------------
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    if (getAccessToken) {
      const token = getAccessToken()
      if (token) {
        config.headers.Authorization = `Bearer ${token}`
      }
    }
    // Attach the active company/tenant ID so the backend knows which tenant to scope to
    if (getTenantId) {
      const tenantId = getTenantId()
      if (tenantId) {
        config.headers['X-Tenant-Id'] = tenantId
      }
    }
    return config
  },
  (error: AxiosError) => Promise.reject(error),
)

// ----------------------------------------------------------------
// Response interceptor — handle 401, trigger refresh + retry
// ----------------------------------------------------------------
apiClient.interceptors.response.use(
  response => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean; _tenantRetry?: boolean }

    // ---------------------------------------------------------------
    // Handle 403 — possibly stale tenant context after company change
    // ---------------------------------------------------------------
    if (error.response?.status === 403 && !originalRequest._tenantRetry && refreshTenantContext) {
      // Don't retry for the /companies/my endpoint itself to avoid loops
      const isCompanyEndpoint = originalRequest.url?.includes('/companies/my')
      if (!isCompanyEndpoint) {
        originalRequest._tenantRetry = true

        // If a tenant refresh is already in progress, wait for it
        if (isTenantRefreshing) {
          return new Promise<void>((resolve) => {
            const prev = onTenantRefreshed
            onTenantRefreshed = () => {
              prev?.()
              resolve()
            }
          }).then(() => apiClient(originalRequest))
        }

        isTenantRefreshing = true
        try {
          const changed = await refreshTenantContext()
          if (changed) {
            // Tenant context was updated — retry the request (interceptor will inject new X-Tenant-Id)
            return apiClient(originalRequest)
          }
        } catch {
          // Tenant refresh failed — fall through to propagate original 403
        } finally {
          isTenantRefreshing = false
          onTenantRefreshed?.()
          onTenantRefreshed = null
        }
      }
      return Promise.reject(mapToApiError(error))
    }

    // Only intercept 401 responses
    if (error.response?.status !== 401) {
      return Promise.reject(mapToApiError(error))
    }

    // Don't retry if:
    // 1. This IS the refresh endpoint (avoid infinite loop)
    // 2. We've already retried this request
    const isRefreshEndpoint = originalRequest.url?.includes('/identity/account/refreshtokendata')
    if (isRefreshEndpoint || originalRequest._retry) {
      onAuthFailure?.()
      return Promise.reject(mapToApiError(error))
    }

    // If another refresh is already in flight, queue this request
    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      })
        .then(newToken => {
          if (originalRequest.headers) {
            originalRequest.headers.Authorization = `Bearer ${newToken}`
          }
          return apiClient(originalRequest)
        })
        .catch(err => Promise.reject(err))
    }

    // Start the refresh flow
    originalRequest._retry = true
    isRefreshing = true

    const refreshToken = storage.getRefreshToken()
    const currentJwt = getAccessToken?.()

    if (!refreshToken || !currentJwt) {
      isRefreshing = false
      processQueue(new Error('No tokens available'))
      onAuthFailure?.()
      return Promise.reject(mapToApiError(error))
    }

    try {
      const refreshPayload: TokenRefreshInfo = {
        jwt: currentJwt,
        refreshToken,
      }

      // Call refresh directly (no interceptors — avoids recursion)
      const response = await axios.post<JWTResponse>(
        `${API_BASE}/identity/account/refreshtokendata`,
        refreshPayload,
        { headers: { 'Content-Type': 'application/json' } },
      )

      const { jwt: newJwt, refreshToken: newRefreshToken } = response.data

      // Persist new refresh token
      storage.setRefreshToken(newRefreshToken)

      // Update auth store's in-memory access token via the provider mechanism
      // The store itself calls setTokenProvider and handles this
      if (window.__authStore__) {
        window.__authStore__.setTokens(newJwt, newRefreshToken)
      }

      // Retry queued requests with new token
      processQueue(null, newJwt)

      // Retry original request
      if (originalRequest.headers) {
        originalRequest.headers.Authorization = `Bearer ${newJwt}`
      }
      return apiClient(originalRequest)
    } catch (refreshError) {
      processQueue(refreshError)
      onAuthFailure?.()
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  },
)

// ----------------------------------------------------------------
// Map Axios errors to typed ApiError
// ----------------------------------------------------------------
function mapToApiError(error: AxiosError): ApiError {
  const status = error.response?.status ?? 0
  const data = error.response?.data as Record<string, unknown> | string | undefined

  let message = ''

  if (typeof data === 'string' && data.length > 0) {
    // Plain-text error response
    message = data
  } else if (data && typeof data === 'object') {
    // Custom RestApiErrorResponse → { error: "..." }
    if (typeof data.error === 'string' && data.error) {
      message = data.error
    }
    // ASP.NET ProblemDetails → { detail: "..." }
    else if (typeof data.detail === 'string' && data.detail) {
      message = data.detail
    }
    // ASP.NET ProblemDetails → { title: "..." }
    else if (typeof data.title === 'string' && data.title) {
      message = data.title
    }
    // ASP.NET ValidationProblemDetails → { errors: { field: ["msg"] } }
    if (!message && data.errors && typeof data.errors === 'object') {
      const entries = Object.values(data.errors as Record<string, string[]>)
      const msgs = entries.flat().filter(Boolean)
      if (msgs.length > 0) {
        message = msgs.join('; ')
      }
    }
  }

  if (!message) {
    message = error.message || 'An unexpected error occurred'
  }

  return new ApiError(status, message)
}

// Allow the auth store to attach itself globally for token updates
declare global {
  interface Window {
    __authStore__?: {
      setTokens: (jwt: string, refreshToken: string) => void
    }
  }
}
