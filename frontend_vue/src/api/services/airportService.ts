// ============================================================
// Airport Service
// Maps to: /api/v1/airports
// ============================================================

import { apiClient } from '@/api/client'
import type { AirportDto } from '@/types/airport'

const BASE = '/airports'

export const airportService = {
  /**
   * GET /api/v1/airports — list all airports
   */
  async getAll(): Promise<AirportDto[]> {
    const { data } = await apiClient.get<AirportDto[]>(BASE)
    return data
  },

  /**
   * GET /api/v1/airports/search?term= — search airports
   */
  async search(term: string): Promise<AirportDto[]> {
    const { data } = await apiClient.get<AirportDto[]>(`${BASE}/search`, { params: { term } })
    return data
  },

  /**
   * GET /api/v1/airports/{id} — get single airport
   */
  async getById(id: string): Promise<AirportDto> {
    const { data } = await apiClient.get<AirportDto>(`${BASE}/${id}`)
    return data
  },
}
