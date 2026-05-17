// ============================================================
// Aircraft Availability Service
// Maps to: /api/v1/aircraft/{aircraftId}/availability
// ============================================================

import { apiClient } from '@/api/client'
import type {
  AircraftAvailabilityDto,
  CreateAircraftAvailabilityDto,
  UpdateAircraftAvailabilityDto,
} from '@/types/availability'

function base(aircraftId: string) {
  return `/aircraft/${aircraftId}/availability`
}

export const availabilityService = {
  /**
   * GET /api/v1/aircraft/{aircraftId}/availability
   */
  async getAll(aircraftId: string): Promise<AircraftAvailabilityDto[]> {
    const { data } = await apiClient.get<AircraftAvailabilityDto[]>(base(aircraftId))
    return data
  },

  /**
   * GET /api/v1/aircraft/{aircraftId}/availability/{id}
   */
  async getById(aircraftId: string, id: string): Promise<AircraftAvailabilityDto> {
    const { data } = await apiClient.get<AircraftAvailabilityDto>(`${base(aircraftId)}/${id}`)
    return data
  },

  /**
   * POST /api/v1/aircraft/{aircraftId}/availability — create availability block (CompanyOwner)
   */
  async create(aircraftId: string, dto: CreateAircraftAvailabilityDto): Promise<AircraftAvailabilityDto> {
    const { data } = await apiClient.post<AircraftAvailabilityDto>(base(aircraftId), dto)
    return data
  },

  /**
   * PUT /api/v1/aircraft/{aircraftId}/availability/{id} — update availability (CompanyOwner)
   */
  async update(aircraftId: string, id: string, dto: UpdateAircraftAvailabilityDto): Promise<AircraftAvailabilityDto> {
    const { data } = await apiClient.put<AircraftAvailabilityDto>(`${base(aircraftId)}/${id}`, dto)
    return data
  },

  /**
   * DELETE /api/v1/aircraft/{aircraftId}/availability/{id} — delete availability (CompanyOwner)
   */
  async delete(aircraftId: string, id: string): Promise<void> {
    await apiClient.delete(`${base(aircraftId)}/${id}`)
  },
}
