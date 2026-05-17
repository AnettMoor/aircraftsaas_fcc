// ============================================================
// Aircraft Photo Service
// Maps to: /api/v1/aircraft/{aircraftId}/photos
// ============================================================

import { apiClient } from '@/api/client'
import type { AircraftPhotoDto } from '@/types/aircraftPhoto'

function base(aircraftId: string) {
  return `/aircraft/${aircraftId}/photos`
}

export const aircraftPhotoService = {
  /**
   * GET /api/v1/aircraft/{aircraftId}/photos
   */
  async getAll(aircraftId: string): Promise<AircraftPhotoDto[]> {
    const { data } = await apiClient.get<AircraftPhotoDto[]>(base(aircraftId))
    return data
  },

  /**
   * POST /api/v1/aircraft/{aircraftId}/photos — upload photo (multipart/form-data, CompanyOwner)
   */
  async upload(aircraftId: string, formData: FormData): Promise<AircraftPhotoDto> {
    const { data } = await apiClient.post<AircraftPhotoDto>(base(aircraftId), formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  /**
   * PUT /api/v1/aircraft/{aircraftId}/photos/{photoId}/set-primary — set primary photo (CompanyOwner)
   */
  async setPrimary(aircraftId: string, photoId: string): Promise<void> {
    await apiClient.put(`${base(aircraftId)}/${photoId}/set-primary`)
  },

  /**
   * DELETE /api/v1/aircraft/{aircraftId}/photos/{photoId} — delete photo (CompanyOwner)
   */
  async delete(aircraftId: string, photoId: string): Promise<void> {
    await apiClient.delete(`${base(aircraftId)}/${photoId}`)
  },
}
