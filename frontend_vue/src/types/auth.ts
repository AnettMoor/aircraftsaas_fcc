// ============================================================
// Auth Types - mirrors ASP.NET Core Identity contracts exactly
// ============================================================

/** Request body for POST /api/v1/identity/account/login */
export interface LoginInfo {
  email: string
  password: string
}

/** Request body for POST /api/v1/identity/account/register */
export interface RegisterInfo {
  email: string
  password: string
  firstname: string
  lastname: string
}

/** Request body for POST /api/v1/identity/account/refreshtokendata */
export interface TokenRefreshInfo {
  jwt: string
  refreshToken: string
}

/** Request body for POST /api/v1/identity/account/logout */
export interface LogoutInfo {
  refreshToken: string
}

/** Response from login, register, refresh */
export interface JWTResponse {
  jwt: string
  refreshToken: string
}

/** Decoded JWT payload claims */
export interface JwtClaims {
  sub: string           // User ID
  email: string         // User email
  given_name?: string   // First name
  family_name?: string  // Last name
  role?: string | string[] // ASP.NET roles
  exp: number           // Expiry unix timestamp
  iat: number           // Issued at unix timestamp
  iss: string           // Issuer
  aud: string           // Audience
}

/** Auth state kept in the Pinia auth store */
export interface AuthState {
  accessToken: string | null  // In-memory only — never persisted
  refreshToken: string | null // Persisted in localStorage
  isRefreshing: boolean
}
