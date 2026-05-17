// ============================================================
// License Types - mirrors App.Application.DTOs.LicenseDtos
// ============================================================

export interface LicenseDto {
  id: string
  appUserId: string
  licenseNumber: string
  licenseType: string
  issueDate: string          // ISO 8601 datetime string
  expiryDate: string         // ISO 8601 datetime string
  issuingAuthority: string
  isValid: boolean
}

export interface CreateLicenseDto {
  licenseNumber: string
  licenseType: string
  issueDate: string          // ISO 8601 datetime string
  expiryDate: string         // ISO 8601 datetime string
  issuingAuthority: string
}

export interface UpdateLicenseDto {
  id: string
  licenseNumber: string
  licenseType: string
  issueDate: string          // ISO 8601 datetime string
  expiryDate: string         // ISO 8601 datetime string
  issuingAuthority: string
}
