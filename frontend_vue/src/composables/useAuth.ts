/**
 * Thin composable wrapper around authStore + sessionStore.
 * Use this in components instead of importing both stores directly.
 */
import { computed } from 'vue'
import { useAuthStore } from '@/stores/authStore'
import { useSessionStore } from '@/stores/sessionStore'

export function useAuth() {
  const authStore = useAuthStore()
  const sessionStore = useSessionStore()

  return {
    // Auth state
    isAuthenticated: computed(() => authStore.isAuthenticated),
    isLoading: computed(() => authStore.isRefreshing), // indicates if a login/logout/register/silentRefresh is in progress

    // Session data
    user: computed(() => sessionStore.user),
    fullName: computed(() => sessionStore.fullName),
    initials: computed(() => sessionStore.initials),
    activeCompany: computed(() => sessionStore.activeCompany),
    isCompanyOwner: computed(() => sessionStore.isCompanyOwner),

    // Actions
    login: authStore.login.bind(authStore),
    logout: authStore.logout.bind(authStore),
    register: authStore.register.bind(authStore),
  }
}
