// ============================================================
// Airport Types - mirrors WebApp.v1.AirportResponse
// ============================================================

export interface AirportDto {
  id: string
  icaoCode: string
  iataCode: string
  name: string
  city: string
  country: string
  latitude: number
  longitude: number
  elevation: number
}
