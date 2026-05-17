// ============================================================
// User / Session Types - mirrors App.Domain.Identity.AppUser
// and App.Domain.AppUserCompany
// ============================================================

/** Matches EAppUserRoleInCompany enum in the backend */
export type AppUserRoleInCompany = 'Normal' | 'CompanyOwner'

export interface AppUserCompanyRef {
  companyId: string
  companyName: string
  companySlug: string
  role: AppUserRoleInCompany
  isActive: boolean
  joinedAt: string
}

/** Current logged-in user profile (decoded from JWT + session) */
export interface UserProfile {
  id: string
  email: string
  firstName: string
  lastName: string
  fullName: string
  companies: AppUserCompanyRef[]
}

/** Active company context */
export interface CompanyContext {
  companyId: string
  companyName: string
  companySlug: string
  role: AppUserRoleInCompany
}
