// ============================================================
// Aircraft Photo Types - mirrors App.Application.DTOs.AircraftDtos :: AircraftPhotoDto
// ============================================================

export interface AircraftPhotoDto {
  id: string
  aircraftId: string
  url: string                // Matches AircraftPhotoResponse.Url from API
  description?: string
  isPrimary: boolean
  displayOrder: number
  uploadedAt: string         // ISO 8601 datetime string
}

export interface AddAircraftPhotoDto {
  url: string
  description?: string
  isPrimary: boolean
  displayOrder: number
}
