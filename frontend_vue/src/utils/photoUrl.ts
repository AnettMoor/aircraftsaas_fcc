// ============================================================
// Photo URL Resolver
//
// Photo URLs returned by the API are relative paths served from
// the backend's wwwroot (e.g. "/uploads/aircraft/{id}/{file}").
// Since the Vue frontend runs on a different origin and the
// reverse proxy only forwards /api/ routes, we route photo
// requests through a dedicated API file-serving endpoint:
//   GET /api/v1/aircraft/{aircraftId}/photos/file?path=...
// ============================================================

import { API_BASE } from '@/api/client'

/**
 * Resolve a photo URL from the API to an absolute URL served
 * through the API file endpoint.
 *
 * Input:  "/uploads/aircraft/{aircraftId}/{filename}.png"
 * Output: "{API_BASE}/aircraft/{aircraftId}/photos/file?path=/uploads/aircraft/{aircraftId}/{filename}.png"
 *
 * If the URL is already absolute (http/https), returns it as-is.
 */
export function resolvePhotoUrl(url: string | undefined | null): string {
  if (!url) return ''
  if (url.startsWith('http://') || url.startsWith('https://')) return url

  // Extract the aircraftId from the path pattern: /uploads/aircraft/{aircraftId}/{filename}
  const match = url.match(/\/uploads\/aircraft\/([^/]+)\//)
  if (match) {
    const aircraftId = match[1]
    return `${API_BASE}/aircraft/${aircraftId}/photos/file?path=${encodeURIComponent(url)}`
  }

  // Fallback: prepend base URL directly (for non-standard paths)
  const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5219'
  const base = API_BASE_URL.replace(/\/+$/, '')
  const path = url.startsWith('/') ? url : `/${url}`
  return `${base}${path}`
}

/**
 * Resolve an array of photo URL strings (e.g. AircraftDto.photoUrls).
 */
export function resolvePhotoUrls(urls: string[] | undefined | null): string[] {
  if (!urls) return []
  return urls.map(resolvePhotoUrl).filter(Boolean)
}
