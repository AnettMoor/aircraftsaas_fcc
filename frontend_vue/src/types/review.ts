// ============================================================
// Review Types - mirrors App.Application.DTOs.ReviewDtos
// ============================================================

export interface ReviewDto {
  id: string
  aircraftId: string
  aircraftName: string
  bookingId: string
  authorId: string
  authorName: string
  rating: number
  comment?: string
  reviewType?: string
  reviewedAt: string
  isVerifiedBooking: boolean
}

export interface CreateReviewDto {
  aircraftId: string
  bookingId: string
  rating: number
  comment?: string
  reviewType?: string
}

export interface UpdateReviewDto {
  rating: number
  comment?: string
  reviewType?: string
}
