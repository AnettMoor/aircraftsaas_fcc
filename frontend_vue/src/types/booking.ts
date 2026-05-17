// ============================================================
// Booking Types - mirrors App.Application.DTOs.BookingDtos
// ============================================================

export type BookingStatus =
  | 'Pending'
  | 'Requested'
  | 'Approved'
  | 'Paid'
  | 'Completed'
  | 'Cancelled'
  | 'Rejected'

export interface BookingDto {
  id: string
  aircraftId: string
  aircraftName: string
  pilotId: string
  pilotName: string
  startDateTime: string  // ISO 8601 datetime string
  endDateTime: string    // ISO 8601 datetime string
  status: BookingStatus
  purpose?: string
  totalAmount: number
  rejectionReason?: string
  approvedAt?: string
  paidAt?: string
  completedAt?: string
  cancelledAt?: string
  companyId: string
  createdAt: string
}

export interface CreateBookingDto {
  aircraftId: string
  startDateTime: string  // ISO 8601 datetime string
  endDateTime: string    // ISO 8601 datetime string
  purpose?: string
}

export interface UpdateBookingDto {
  id: string
  startDateTime: string  // ISO 8601 datetime string
  endDateTime: string    // ISO 8601 datetime string
  purpose?: string
}

export interface PaymentDto {
  paymentMethod: string
  transactionId?: string
  paymentDetails?: string
}
