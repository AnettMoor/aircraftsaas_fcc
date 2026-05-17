// ============================================================
// Booking Service
// Maps to: /api/v1/bookings
// ============================================================

import { apiClient } from '@/api/client'
import type { BookingDto, BookingStatus, CreateBookingDto, UpdateBookingDto, PaymentDto } from '@/types/booking'

const BASE = '/bookings'

/**
 * Backend EBookingStatus enum maps integers → string names.
 * If the backend does NOT have JsonStringEnumConverter, status comes as an integer.
 * This map normalises it to the expected string value.
 */
const STATUS_MAP: Record<number, BookingStatus> = {
  0: 'Pending',
  1: 'Requested',
  2: 'Approved',
  3: 'Paid',
  4: 'Completed',
  5: 'Cancelled',
  6: 'Rejected',
}

function normalizeStatus(status: unknown): BookingStatus {
  if (typeof status === 'string') return status as BookingStatus
  if (typeof status === 'number' && status in STATUS_MAP) return STATUS_MAP[status]
  return 'Pending' // fallback
}

function normalizeBooking(b: BookingDto): BookingDto {
  return { ...b, status: normalizeStatus(b.status) }
}

export const bookingService = {
  /**
   * GET /api/v1/bookings/company — all bookings for current company (admin)
   */
  async getAll(): Promise<BookingDto[]> {
    const { data } = await apiClient.get<BookingDto[]>(`${BASE}/company`)
    return (data ?? []).map(normalizeBooking)
  },

  /**
   * GET /api/v1/bookings/{id}
   */
  async getById(id: string): Promise<BookingDto> {
    const { data } = await apiClient.get<BookingDto>(`${BASE}/${id}`)
    return normalizeBooking(data)
  },

  /**
   * GET /api/v1/bookings/my — current user's bookings (as pilot)
   */
  async getMy(): Promise<BookingDto[]> {
    const { data } = await apiClient.get<BookingDto[]>(`${BASE}/my`)
    return (data ?? []).map(normalizeBooking)
  },

  /**
   * POST /api/v1/bookings — create a booking request
   */
  async create(dto: CreateBookingDto): Promise<BookingDto> {
    const { data } = await apiClient.post<BookingDto>(BASE, dto)
    return normalizeBooking(data)
  },

  /**
   * PUT /api/v1/bookings/{id} — update a booking (pilot, Pending/Requested only)
   */
  async update(id: string, dto: UpdateBookingDto): Promise<BookingDto> {
    const { data } = await apiClient.put<BookingDto>(`${BASE}/${id}`, dto)
    return normalizeBooking(data)
  },

  /**
   * POST /api/v1/bookings/{id}/approve (CompanyOwner)
   */
  async approve(id: string): Promise<BookingDto> {
    const { data } = await apiClient.post<BookingDto>(`${BASE}/${id}/approve`)
    return normalizeBooking(data)
  },

  /**
   * POST /api/v1/bookings/{id}/reject (CompanyOwner)
   */
  async reject(id: string, reason: string): Promise<BookingDto> {
    const { data } = await apiClient.post<BookingDto>(`${BASE}/${id}/reject`, { reason })
    return normalizeBooking(data)
  },

  /**
   * POST /api/v1/bookings/{id}/cancel
   */
  async cancel(id: string): Promise<BookingDto> {
    const { data } = await apiClient.post<BookingDto>(`${BASE}/${id}/cancel`)
    return normalizeBooking(data)
  },

  /**
   * POST /api/v1/bookings/{id}/pay — submit payment
   */
  async pay(id: string, dto: PaymentDto): Promise<BookingDto> {
    const { data } = await apiClient.post<BookingDto>(`${BASE}/${id}/pay`, dto)
    return normalizeBooking(data)
  },

  /**
   * POST /api/v1/bookings/{id}/complete (CompanyOwner)
   */
  async complete(id: string): Promise<BookingDto> {
    const { data } = await apiClient.post<BookingDto>(`${BASE}/${id}/complete`)
    return normalizeBooking(data)
  },
}
