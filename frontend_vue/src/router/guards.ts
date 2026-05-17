// ============================================================
// Navigation Guards
//
// Guard order:
// 1. Ensure session is initialized (attempt silent refresh on cold start)
// 2. guestOnly routes → redirect authenticated users to dashboard
// 3. requiresAuth routes → redirect guests to login
// 4. requiresRole routes → redirect insufficient-role users to client dashboard
// ============================================================

import type { Router, RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useSessionStore } from '@/stores/sessionStore'

export function setupGuards(router: Router): void {
  router.beforeEach(async (to: RouteLocationNormalized, _from: RouteLocationNormalized) => {
    const authStore = useAuthStore()
    const sessionStore = useSessionStore()

    // ----------------------------------------------------------------
    // Step 1: Initialize session on first navigation
    // ----------------------------------------------------------------
    if (!authStore.sessionInitialized) {
      // Attempt silent refresh — restores session from refresh token + jwt hint
      await authStore.silentRefresh()
    }

    const isAuthenticated = authStore.isAuthenticated

    // ----------------------------------------------------------------
    // Step 2: Guest-only routes — kick authenticated users to dashboard
    // ----------------------------------------------------------------
    if (to.meta.guestOnly && isAuthenticated) {
      return sessionStore.isCompanyOwner
        ? { name: 'admin-dashboard' }
        : { name: 'client-dashboard' }
    }

    // ----------------------------------------------------------------
    // Step 3: Protected routes — redirect unauthenticated users to login
    // ----------------------------------------------------------------
    if (to.meta.requiresAuth && !isAuthenticated) {
      return {
        name: 'login',
        query: { redirect: to.fullPath }, // remember where they were going
      }
    }

    // ----------------------------------------------------------------
    // Step 4: Role-based access — CompanyOwner only routes
    // ----------------------------------------------------------------
    if (to.meta.requiresRole === 'CompanyOwner' && isAuthenticated) {
      if (!sessionStore.isCompanyOwner) {
        // User is authenticated but lacks the required role
        return { name: 'client-dashboard' }
      }
    }

    // ----------------------------------------------------------------
    // Step 4b: CompanyOwners must not access client/normal-user routes
    // ----------------------------------------------------------------
    if (isAuthenticated && sessionStore.isCompanyOwner && to.path.startsWith('/client')) {
      return { name: 'admin-dashboard' }
    }

    // ----------------------------------------------------------------
    // Step 5: Update document title from route meta
    // ----------------------------------------------------------------
    const title = to.meta.title as string | undefined
    if (title) {
      document.title = `${title} | AircraftSaaS`
    } else {
      document.title = 'AircraftSaaS'
    }

    return true
  })
}
