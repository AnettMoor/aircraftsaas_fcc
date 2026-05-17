// ============================================================
// Insurance Policy Service
// Maps to: /api/v1/aircraft/{aircraftId}/insurance
// ============================================================

import { apiClient } from '@/api/client'
import type {
  InsurancePolicyDto,
  CreateInsurancePolicyDto,
  UpdateInsurancePolicyDto,
} from '@/types/insurance'

function base(aircraftId: string) {
  return `/aircraft/${aircraftId}/insurance`
}

export const insuranceService = {
  /**
   * GET /api/v1/aircraft/{aircraftId}/insurance
   */
  async getAll(aircraftId: string): Promise<InsurancePolicyDto[]> {
    const { data } = await apiClient.get<InsurancePolicyDto[]>(base(aircraftId))
    return data
  },

  /**
   * GET /api/v1/aircraft/{aircraftId}/insurance/{id}
   */
  async getById(aircraftId: string, id: string): Promise<InsurancePolicyDto> {
    const { data } = await apiClient.get<InsurancePolicyDto>(`${base(aircraftId)}/${id}`)
    return data
  },

  /**
   * POST /api/v1/aircraft/{aircraftId}/insurance — create policy (CompanyOwner)
   */
  async create(aircraftId: string, dto: CreateInsurancePolicyDto): Promise<InsurancePolicyDto> {
    const { data } = await apiClient.post<InsurancePolicyDto>(base(aircraftId), dto)
    return data
  },

  /**
   * PUT /api/v1/aircraft/{aircraftId}/insurance/{id} — update policy (CompanyOwner)
   */
  async update(aircraftId: string, id: string, dto: UpdateInsurancePolicyDto): Promise<InsurancePolicyDto> {
    const { data } = await apiClient.put<InsurancePolicyDto>(`${base(aircraftId)}/${id}`, dto)
    return data
  },

  /**
   * DELETE /api/v1/aircraft/{aircraftId}/insurance/{id} — delete policy (CompanyOwner)
   */
  async delete(aircraftId: string, id: string): Promise<void> {
    await apiClient.delete(`${base(aircraftId)}/${id}`)
  },
}
