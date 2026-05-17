// ============================================================
// License Service
// Maps to: /api/v1/licenses
// ============================================================

import { apiClient } from '@/api/client'
import type { LicenseDto, CreateLicenseDto, UpdateLicenseDto } from '@/types/license'

const BASE = '/licenses'

export const licenseService = {
  /**
   * GET /api/v1/licenses — list current user's pilot licenses
   */
  async getAll(): Promise<LicenseDto[]> {
    const { data } = await apiClient.get<LicenseDto[]>(BASE)
    return data
  },

  /**
   * GET /api/v1/licenses/{id}
   */
  async getById(id: string): Promise<LicenseDto> {
    const { data } = await apiClient.get<LicenseDto>(`${BASE}/${id}`)
    return data
  },

  /**
   * POST /api/v1/licenses — create a license (authenticated)
   */
  async create(dto: CreateLicenseDto): Promise<LicenseDto> {
    const { data } = await apiClient.post<LicenseDto>(BASE, dto)
    return data
  },

  /**
   * PUT /api/v1/licenses/{id} — update a license (owner only)
   */
  async update(id: string, dto: UpdateLicenseDto): Promise<LicenseDto> {
    const { data } = await apiClient.put<LicenseDto>(`${BASE}/${id}`, dto)
    return data
  },

  /**
   * DELETE /api/v1/licenses/{id} — soft-delete a license (owner only)
   */
  async delete(id: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}`)
  },
}
