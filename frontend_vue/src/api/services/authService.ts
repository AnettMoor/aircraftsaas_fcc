// ============================================================
// Auth Service
// Maps to: POST /api/v1/identity/account/{action}
// ============================================================

import { apiClient } from '@/api/client'
import type { LoginInfo, RegisterInfo, JWTResponse, TokenRefreshInfo, LogoutInfo } from '@/types/auth'

const BASE = '/identity/account'

export const authService = {
  /**
   * POST /api/v1/identity/account/login
   */
  async login(credentials: LoginInfo): Promise<JWTResponse> {
    const { data } = await apiClient.post<JWTResponse>(`${BASE}/login`, credentials)
    return data
  },

  /**
   * POST /api/v1/identity/account/register
   */
  async register(info: RegisterInfo): Promise<JWTResponse> {
    const { data } = await apiClient.post<JWTResponse>(`${BASE}/register`, info)
    return data
  },

  /**
   * POST /api/v1/identity/account/refreshtokendata
   * Note: This is called directly without the Axios interceptor in client.ts
   * to avoid infinite refresh loops. The interceptor imports this too.
   */
  async refreshToken(payload: TokenRefreshInfo): Promise<JWTResponse> {
    const { data } = await apiClient.post<JWTResponse>(`${BASE}/refreshtokendata`, payload)
    return data
  },

  /**
   * POST /api/v1/identity/account/logout
   * Requires valid JWT Bearer token (will be added by Axios interceptor).
   */
  async logout(payload: LogoutInfo): Promise<void> {
    await apiClient.post(`${BASE}/logout`, payload)
  },
}
