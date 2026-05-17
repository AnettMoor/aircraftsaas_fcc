// ============================================================
// Company Types - mirrors App.Application.DTOs.CompanyDtos
// ============================================================

export type SubscriptionTier = 'Free' | 'Basic' | 'Professional' | 'Enterprise'

export interface CompanyDto {
  id: string
  companyName: string
  slug: string
  subscriptionTier: SubscriptionTier
  subscriptionExpiresAt?: string
  isActive: boolean
  maxUsers: number
  maxAircraft: number
  maxBookingsPerMonth: number
  address?: string
  phone?: string
  email?: string
  currentUserCount: number
  currentAircraftCount: number
  createdAt: string
}

export interface CreateCompanyDto {
  companyName: string
  address?: string
  phone?: string
  email?: string
}

export interface UpdateCompanyDto {
  companyName: string
  address?: string
  phone?: string
  email?: string
}
