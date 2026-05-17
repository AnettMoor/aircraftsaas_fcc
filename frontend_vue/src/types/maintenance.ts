// ============================================================
// Maintenance Types - mirrors App.Application.DTOs.MaintenanceDtos
// ============================================================

export interface MaintenanceRecordDto {
  id: string
  aircraftId: string
  aircraftName: string
  maintenanceDate: string    // ISO 8601 datetime string
  startDate?: string         // ISO 8601 datetime string — maintenance timeframe start
  endDate?: string           // ISO 8601 datetime string — maintenance timeframe end
  maintenanceType: string
  status: string             // Scheduled, InProgress, Completed, Cancelled
  description: string
  performedBy: string
  airframeHoursAtMaintenance: number
  nextDueDate?: string       // ISO 8601 datetime string
  nextDueHours?: number
  cost: number
  isCompleted: boolean
  createdAt: string          // ISO 8601 datetime string
}

export interface CreateMaintenanceRecordDto {
  aircraftId: string
  maintenanceDate: string    // ISO 8601 datetime string
  startDate?: string         // ISO 8601 datetime string — maintenance timeframe start
  endDate?: string           // ISO 8601 datetime string — maintenance timeframe end
  maintenanceType: string
  description?: string
  performedBy?: string
  airframeHoursAtMaintenance: number
  nextDueDate?: string       // ISO 8601 datetime string
  nextDueHours?: number
  cost: number
  isCompleted: boolean
}

export interface UpdateMaintenanceRecordDto {
  id: string
  aircraftId: string
  maintenanceDate: string    // ISO 8601 datetime string
  startDate?: string         // ISO 8601 datetime string — maintenance timeframe start
  endDate?: string           // ISO 8601 datetime string — maintenance timeframe end
  maintenanceType: string
  description?: string
  performedBy?: string
  airframeHoursAtMaintenance: number
  nextDueDate?: string       // ISO 8601 datetime string
  nextDueHours?: number
  cost: number
  isCompleted: boolean
}
