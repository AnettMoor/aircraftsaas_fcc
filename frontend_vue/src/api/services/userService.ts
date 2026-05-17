// ============================================================
// User/Profile Service
// Maps to: /api/v1/companies/my
// ============================================================

import { apiClient } from '@/api/client'
import { getRolesFromToken } from '@/utils/jwt'
import { storage } from '@/utils/storage'
import type { AppUserCompanyRef } from '@/types/user'
import type { CompanyDto } from '@/types/company'

export const userService = {
  /**
   * GET /api/v1/companies/my — get the current user's company.
   * Returns a single CompanyDto for the current tenant context.
   */
  async getMyCompany(): Promise<CompanyDto> {
    const { data } = await apiClient.get<CompanyDto>('/companies/my')
    return data
  },

  /**
   * GET /api/v1/companies/my — get current user's company as a single-item array
   * for compatibility with session store AppUserCompanyRef[] shape.
   * Maps CompanyDto fields to AppUserCompanyRef.
   * Role is resolved from the current JWT token claims.
   */
  async getMyCompanies(): Promise<AppUserCompanyRef[]> {
    const { data } = await apiClient.get<CompanyDto>('/companies/my')

    // Determine company role from JWT claims
    const jwt = storage.getJwt()
    const roles = jwt ? getRolesFromToken(jwt) : []
    const role = roles.includes('CompanyOwner') ? 'CompanyOwner' : 'Normal'

    const ref: AppUserCompanyRef = {
      companyId: data.id,
      companyName: data.companyName,
      companySlug: data.slug,
      role,
      isActive: data.isActive,
      joinedAt: data.createdAt,
    }
    return [ref]
  },
}
