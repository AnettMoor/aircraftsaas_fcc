// ============================================================
// Company Service
// Maps to: /api/v1/companies
// ============================================================

import { apiClient } from '@/api/client'
import type { CompanyDto, CreateCompanyDto, UpdateCompanyDto } from '@/types/company'

const BASE = '/companies'

export const companyService = {
  /**
   * GET /api/v1/companies — all companies for current user
   */
  async getAll(): Promise<CompanyDto[]> {
    const { data } = await apiClient.get<CompanyDto[]>(BASE)
    return data
  },

  /**
   * GET /api/v1/companies/{id}
   */
  async getById(id: string): Promise<CompanyDto> {
    const { data } = await apiClient.get<CompanyDto>(`${BASE}/${id}`)
    return data
  },

  /**
   * POST /api/v1/companies — create a new company
   */
  async create(dto: CreateCompanyDto): Promise<CompanyDto> {
    const { data } = await apiClient.post<CompanyDto>(BASE, dto)
    return data
  },

  /**
   * PUT /api/v1/companies/{id} — update company details (CompanyOwner)
   */
  async update(id: string, dto: UpdateCompanyDto): Promise<CompanyDto> {
    const { data } = await apiClient.put<CompanyDto>(`${BASE}/${id}`, dto)
    return data
  },

  /**
   * GET /api/v1/companies/my — get current user's company
   */
  async getMy(): Promise<CompanyDto> {
    const { data } = await apiClient.get<CompanyDto>(`${BASE}/my`)
    return data
  },

  /**
   * DELETE /api/v1/companies/{id}
   */
  async delete(id: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}`)
  },
}
