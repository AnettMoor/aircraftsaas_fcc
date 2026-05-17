// ============================================================
// Review Service
// Maps to: /api/v1/reviews
// ============================================================

import { apiClient } from '@/api/client'
import type { ReviewDto, CreateReviewDto, UpdateReviewDto } from '@/types/review'

const BASE = '/reviews'

export const reviewService = {
  /**
   * GET /api/v1/review — get all reviews (public)
   */
  async getAll(): Promise<ReviewDto[]> {
    const { data } = await apiClient.get<ReviewDto[]>(BASE)
    return data
  },

  /**
   * GET /api/v1/review/{id} — get a single review (public)
   */
  async getById(id: string): Promise<ReviewDto> {
    const { data } = await apiClient.get<ReviewDto>(`${BASE}/${id}`)
    return data
  },

  /**
   * GET /api/v1/review/aircraft/{aircraftId} — get reviews for an aircraft (public)
   */
  async getByAircraft(aircraftId: string): Promise<ReviewDto[]> {
    const { data } = await apiClient.get<ReviewDto[]>(`${BASE}/aircraft/${aircraftId}`)
    return data
  },

  /**
   * GET /api/v1/review/aircraft/{aircraftId}/rating — get average rating (public)
   */
  async getAircraftRating(aircraftId: string): Promise<number> {
    const { data } = await apiClient.get<number>(`${BASE}/aircraft/${aircraftId}/rating`)
    return data
  },

  /**
   * POST /api/v1/review — create a review (authenticated, must have completed booking)
   */
  async create(dto: CreateReviewDto): Promise<ReviewDto> {
    const { data } = await apiClient.post<ReviewDto>(BASE, dto)
    return data
  },

  /**
   * PUT /api/v1/review/{id} — update a review (author only)
   */
  async update(id: string, dto: UpdateReviewDto): Promise<ReviewDto> {
    const { data } = await apiClient.put<ReviewDto>(`${BASE}/${id}`, dto)
    return data
  },

  /**
   * DELETE /api/v1/review/{id} — delete a review (author only)
   */
  async delete(id: string): Promise<void> {
    await apiClient.delete(`${BASE}/${id}`)
  },
}
