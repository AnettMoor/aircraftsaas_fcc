// ============================================================
// Aircraft Availability Types - mirrors App.Application.DTOs.AircraftAvailabilityDtos
// ============================================================

export type AvailabilityType = 'Available' | 'Blocked' | 'Maintenance' | 'Booked' | 'NoInsurance'

export interface AircraftAvailabilityDto {
  id: string
  aircraftId: string
  startDateTime: string      // ISO 8601 datetime string
  endDateTime: string        // ISO 8601 datetime string
  availabilityType: AvailabilityType
  reason?: string
}

export interface CreateAircraftAvailabilityDto {
  startDateTime: string      // ISO 8601 datetime string
  endDateTime: string        // ISO 8601 datetime string
  availabilityType: string
  reason?: string
}

export interface UpdateAircraftAvailabilityDto {
  id: string
  startDateTime: string      // ISO 8601 datetime string
  endDateTime: string        // ISO 8601 datetime string
  availabilityType: string
  reason?: string
}
