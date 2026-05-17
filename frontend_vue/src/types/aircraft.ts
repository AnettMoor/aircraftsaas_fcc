// ============================================================
// Aircraft Types - mirrors App.Application.DTOs.AircraftDtos
// ============================================================

import type { InsurancePolicyDto } from './insurance'

export type AircraftStatus = 'Available' | 'Unavailable' | 'InsuranceInactive' | 'Maintenance'

export interface AircraftDto {
  id: string
  registrationNumber: string
  make: string
  model: string
  year: number
  category: string
  requiredLicenseType: string
  totalAirspeedHours: number
  hourlyRate: number
  baseAirportId: string
  baseAirportName: string
  description: string
  isAvailable: boolean
  companyId: string
  companyName: string
  companyEmail?: string
  companyPhone?: string
  photoUrls: string[]
  averageRating: number
  reviewCount: number
  isInsured: boolean
  insuranceExpiryDate?: string
  hasActiveMaintenance: boolean
  status: AircraftStatus
  /** All insurance policies (active & future) — used for per-day calendar checks */
  insurancePolicies: InsurancePolicyDto[]
}

export interface CreateAircraftDto {
  registrationNumber: string
  make: string
  model: string
  year: number
  category: string
  requiredLicenseType: string
  totalAirspeedHours: number
  hourlyRate: number
  baseAirportId: string
  description: string
}

export interface UpdateAircraftDto {
  id: string
  registrationNumber: string
  make: string
  model: string
  year: number
  category: string
  requiredLicenseType: string
  totalAirspeedHours: number
  hourlyRate: number
  baseAirportId: string
  description: string
  isAvailable: boolean
}

export interface AircraftSearchParams {
  make?: string
  model?: string
  category?: string
  location?: string
  status?: string
  startDate?: string
  endDate?: string
  maxHourlyRate?: number
  year?: number
  page?: number
  pageSize?: number
}
