// ============================================================
// Aircraft Service
// Maps to: /api/v1/aircraft
// ============================================================

import { apiClient } from '@/api/client'
import type { AircraftDto, CreateAircraftDto, UpdateAircraftDto, AircraftSearchParams } from '@/types/aircraft'

const BASE = '/aircraft'

export const aircraftService = {
  /**
   * GET /api/v1/aircaft — public catalog search
   */
  async search(params?: AircraftSearchParams): Promise<AircraftDto[]> {
    const { data } = await apiClient.get<AircraftDto[]>(BASE, { params })
    return data
  },

  /**
   * GET /api/v1/aircaft/available — available aircraft for time range
   */
  async getAvailable(start: string, end: string, location?: string): Promise<AircraftDto[]> {
    const { data } = await apiClient.get<AircraftDto[]>(`${BASE}/available`, {
      params: { start, end, location },
    })
    return data
  },

  /**
   * GET /api/v1/aircaft/{id}
   */
  async getById(id: string): Promise<AircraftDto> {
    const { data } = await apiClient.get<AircraftDto>(`${BASE}/${id}`)
    return data
  },

  /**
   * GET /api/v1/aircaft/company — all aircraft for current user's company
   * Requires authentication.
   */
  async getCompanyAircraft(): Promise<AircraftDto[]> {
    const { data } = await apiClient.get<AircraftDto[]>(`${BASE}/company`)
    return data
  },

  /**
   * GET /api/v1/aircraft/company/deleted — deactivated (soft-deleted) aircraft for current user's company
   * Requires CompanyOwner role.
   */
  async getCompanyDeletedAircraft(): Promise<AircraftDto[]> {
    const { data } = await apiClient.get<AircraftDto[]>(`${BASE}/company/deleted`)
    return data
  },

  /**
   * POST /api/v1/aircaft — create aircraft (CompanyOwner only)
   */
  async create(dto: CreateAircraftDto): Promise<AircraftDto> {
    const { data } = await apiClient.post<AircraftDto>(BASE, dto)
    return data
  },

  /**
   * PUT /api/v1/aircaft/{id} — update aircraft (CompanyOwner only)
   */
  async update(id: string, dto: UpdateAircraftDto): Promise<AircraftDto> {
    const { data } = await apiClient.put<AircraftDto>(`${BASE}/${id}`, dto)
    return data
  },

  /**
   * DELETE /api/v1/aircaft/{id} — soft delete (CompanyOwner only)
   */
  async delete(id: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}`)
  },

  /**
   * POST /api/v1/aircaft/{id}/restore — restore soft-deleted (CompanyOwner only)
   */
  async restore(id: string): Promise<void> {
    await apiClient.post(`${BASE}/${id}/restore`)
  },
}
