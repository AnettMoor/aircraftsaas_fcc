// ============================================================
// Maintenance Service
// Maps to: /api/v1/maintenance
// ============================================================

import { apiClient } from '@/api/client'
import type {
  MaintenanceRecordDto,
  CreateMaintenanceRecordDto,
  UpdateMaintenanceRecordDto,
} from '@/types/maintenance'

const BASE = '/maintenance'

export const maintenanceService = {
  /**
   * GET /api/v1/maintenance — list all maintenance records, optional aircraftId filter
   */
  async getAll(aircraftId?: string): Promise<MaintenanceRecordDto[]> {
    const { data } = await apiClient.get<MaintenanceRecordDto[]>(BASE, {
      params: aircraftId ? { aircraftId } : undefined,
    })
    return data
  },

  /**
   * GET /api/v1/maintenance/{id}
   */
  async getById(id: string): Promise<MaintenanceRecordDto> {
    const { data } = await apiClient.get<MaintenanceRecordDto>(`${BASE}/${id}`)
    return data
  },

  /**
   * POST /api/v1/maintenance — create a maintenance record (CompanyOwner)
   */
  async create(dto: CreateMaintenanceRecordDto): Promise<MaintenanceRecordDto> {
    const { data } = await apiClient.post<MaintenanceRecordDto>(BASE, dto)
    return data
  },

  /**
   * PUT /api/v1/maintenance/{id} — update a maintenance record (CompanyOwner)
   */
  async update(id: string, dto: UpdateMaintenanceRecordDto): Promise<MaintenanceRecordDto> {
    const { data } = await apiClient.put<MaintenanceRecordDto>(`${BASE}/${id}`, dto)
    return data
  },

  /**
   * DELETE /api/v1/maintenance/{id} — soft-delete a maintenance record (CompanyOwner)
   */
  async delete(id: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}`)
  },
}
