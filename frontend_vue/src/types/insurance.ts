// ============================================================
// Insurance Policy Types - mirrors App.Application.DTOs.InsurancePolicyDtos
// ============================================================

export interface InsurancePolicyDto {
  id: string
  aircraftId: string
  policyNumber: string
  insuranceProvider: string
  startDate: string          // ISO 8601 datetime string
  endDate: string            // ISO 8601 datetime string
  coverageAmount: number
  coverageType: string
  isActive: boolean
}

export interface CreateInsurancePolicyDto {
  policyNumber: string
  insuranceProvider: string
  startDate: string          // ISO 8601 datetime string
  endDate: string            // ISO 8601 datetime string
  coverageAmount: number
  coverageType: string
}

export interface UpdateInsurancePolicyDto {
  id: string
  policyNumber: string
  insuranceProvider: string
  startDate: string          // ISO 8601 datetime string
  endDate: string            // ISO 8601 datetime string
  coverageAmount: number
  coverageType: string
}
