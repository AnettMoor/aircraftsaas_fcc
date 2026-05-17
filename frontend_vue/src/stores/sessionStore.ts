// ============================================================
// Pinia Session Store
//
// Responsibilities:
// - Store the currently authenticated user's profile
// - Track the active company context
// - Provide role-based getter helpers
// - Hydrate from JWT claims after login/refresh
// ============================================================

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { decodeJwt } from '@/utils/jwt'
import type { UserProfile, CompanyContext, AppUserRoleInCompany, AppUserCompanyRef } from '@/types/user'

export const useSessionStore = defineStore('session', () => {
  // ----------------------------------------------------------------
  // State
  // ----------------------------------------------------------------
  const user = ref<UserProfile | null>(null)
  const activeCompany = ref<CompanyContext | null>(null)
  /** Roles extracted from the JWT claims (ASP.NET Identity roles) */
  const jwtRoles = ref<string[]>([])

  // ----------------------------------------------------------------
  // Getters
  // ----------------------------------------------------------------
  const isCompanyOwner = computed(() => {
    // Check activeCompany role first, then fall back to JWT role claim
    if (activeCompany.value?.role === 'CompanyOwner') return true
    return jwtRoles.value.includes('CompanyOwner')
  })

  const fullName = computed(() => {
    if (!user.value) return ''
    return `${user.value.firstName} ${user.value.lastName}`.trim()
  })

  const initials = computed(() => {
    if (!user.value) return ''
    return `${user.value.firstName?.charAt(0) ?? ''}${user.value.lastName?.charAt(0) ?? ''}`.toUpperCase()
  })

  const userCompanies = computed((): AppUserCompanyRef[] => {
    return user.value?.companies ?? []
  })

  const currentRole = computed((): AppUserRoleInCompany | null => {
    return activeCompany.value?.role ?? null
  })


  // ----------------------------------------------------------------
  // Actions
  // ----------------------------------------------------------------

  /**
   * Hydrate user profile from decoded JWT claims.
   * The JWT from this backend includes: sub, email, given_name, family_name, role
   */
  function hydrateFromJwt(jwt: string): void {
    const claims = decodeJwt(jwt)
    if (!claims) return

    const userId = claims.sub
    const email = claims.email
    // ASP.NET Core Identity uses these standard claim types
    const claimsAny = claims as unknown as Record<string, unknown>
    const firstName = (claimsAny['given_name'] as string) ?? ''
    const lastName = (claimsAny['family_name'] as string) ?? ''

    // Extract roles from JWT (ASP.NET Identity emits as 'role' claim)
    const rawRole = claims.role
    jwtRoles.value = rawRole
      ? Array.isArray(rawRole) ? rawRole : [rawRole]
      : []

    // Update minimal profile from token
    // Full company list should be fetched separately via API if needed
    if (!user.value || user.value.id !== userId) {
      user.value = {
        id: userId,
        email,
        firstName,
        lastName,
        fullName: `${firstName} ${lastName}`.trim(),
        companies: user.value?.companies ?? [],
      }
    } else {
      // Update email/name if changed
      user.value = {
        ...user.value,
        email,
        firstName,
        lastName,
        fullName: `${firstName} ${lastName}`.trim(),
      }
    }
  }

  /**
   * Set the full user profile (called after fetching profile from API).
   */
  function setUserProfile(profile: UserProfile): void {
    user.value = profile

    // Auto-select first active company if none selected
    if (!activeCompany.value && profile.companies.length > 0) {
      const firstActive = profile.companies.find(c => c.isActive) ?? profile.companies[0]
      setActiveCompany(firstActive)
    }
  }

  /**
   * Set the active company context.
   */
  function setActiveCompany(company: AppUserCompanyRef): void {
    activeCompany.value = {
      companyId: company.companyId,
      companyName: company.companyName,
      companySlug: company.companySlug,
      role: company.role,
    }
  }

  /**
   * Switch to a different company the user belongs to.
   */
  function switchCompany(companyId: string): boolean {
    const company = user.value?.companies.find(c => c.companyId === companyId)
    if (!company) return false
    setActiveCompany(company)
    return true
  }

  function clearSession(): void {
    user.value = null
    activeCompany.value = null
    jwtRoles.value = []
  }

  return {
    // state
    user,
    activeCompany,
    jwtRoles,
    // getters
    isCompanyOwner,
    fullName,
    initials,
    userCompanies,
    currentRole,
    // actions
    hydrateFromJwt,
    setUserProfile,
    setActiveCompany,
    switchCompany,
    clearSession,
  }
})
