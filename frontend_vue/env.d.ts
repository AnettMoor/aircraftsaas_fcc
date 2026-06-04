/// <reference types="vite/client" />

interface ImportMetaEnv {
  // Legacy single-origin base — kept for back-compat with code paths
  // that haven't been migrated to the per-service router yet.
  readonly VITE_API_BASE_URL: string
  readonly VITE_API_VERSION: string
  readonly VITE_APP_TITLE: string

  // Per-microservice base URLs. The Vue HTTP client picks the right
  // one for each request path. Empty string means "same-origin /
  // dev-server proxy" (used in `npm run dev`).
  readonly VITE_USERS_URL?: string
  readonly VITE_FLEET_URL?: string
  readonly VITE_BOOKING_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
