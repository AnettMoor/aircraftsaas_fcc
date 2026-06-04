// ============================================================
// Photo URL Resolver
//
// Photo URLs returned by the API are relative paths served from
// the backend's wwwroot (e.g. "/uploads/aircraft/{id}/{file}").
// Since the Vue frontend runs on a different origin (and even on
// a different microservice subdomain) we route photo requests
// through a dedicated API file-serving endpoint:
//   GET /api/v1/aircraft/{aircraftId}/photos/file?path=...
//
// In per-service routing mode (Option B), aircraft photos live
// on the Fleet microservice subdomain, so we use `resolveApiBase`
// to compute the absolute base URL for the request rather than
// the legacy single-origin `API_BASE`.
// ============================================================

import { resolveApiBase } from '@/api/client'

/**
 * Resolve a photo URL from the API to an absolute URL served
 * through the API file endpoint.
 *
 * Input:  "/uploads/aircraft/{aircraftId}/{filename}.png"
 * Output: "{fleet-base}/aircraft/{aircraftId}/photos/file?path=/uploads/aircraft/{aircraftId}/{filename}.png"
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
    // `/aircraft/...` routes to the Fleet microservice in per-service mode.
    const base = resolveApiBase('/aircraft')
    return `${base}/aircraft/${aircraftId}/photos/file?path=${encodeURIComponent(url)}`
  }

  // Fallback: prepend Fleet origin directly (for non-standard paths).
  const fallbackBase = resolveApiBase('/aircraft').replace(/\/api\/v\d+$/, '')
  const path = url.startsWith('/') ? url : `/${url}`
  return `${fallbackBase}${path}`
}

/**
 * Resolve an array of photo URL strings (e.g. AircraftDto.photoUrls).
 */
export function resolvePhotoUrls(urls: string[] | undefined | null): string[] {
  if (!urls) return []
  return urls.map(resolvePhotoUrl).filter(Boolean)
}
