<template>
  <div class="flex min-h-screen bg-slate-50">
    <!-- Sidebar -->
    <AppSidebar
      :sections="navSections"
      :mobile-open="mobileOpen"
      @update:mobile-open="mobileOpen = $event"
      @logout="handleLogout"
    />

    <!-- Main content -->
    <div class="flex-1 flex flex-col min-w-0">
      <!-- Top bar -->
      <header class="h-14 bg-white/80 backdrop-blur-md border-b border-slate-200/80 flex items-center px-6 gap-4 sticky top-0 z-30">
        <!-- Mobile hamburger -->
        <button
          class="md:hidden flex items-center justify-center w-9 h-9 rounded-lg text-slate-500 hover:text-slate-700 hover:bg-slate-100 transition-all duration-200 border-none bg-transparent cursor-pointer"
          @click="mobileOpen = true"
        >
          <Menu class="w-5 h-5" />
        </button>

        <div class="flex-1">
          <span class="font-semibold text-[15px] text-slate-800 tracking-tight">{{ currentPageTitle }}</span>
        </div>

        <RouterLink to="/client/profile" class="flex items-center gap-3 no-underline p-1.5 px-2.5 rounded-xl transition-colors duration-200 hover:bg-slate-100">
          <span class="text-sm text-slate-600 font-medium hidden sm:inline">{{ userEmail }}</span>
          <div class="w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-indigo-600 text-white flex items-center justify-center text-[11px] font-bold ring-2 ring-blue-500/15">
            {{ userInitials }}
          </div>
        </RouterLink>
      </header>

      <!-- Page content -->
      <main class="flex-1 p-6 overflow-y-auto">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useSessionStore } from '@/stores/sessionStore'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import type { NavSection } from '@/components/layout/AppSidebar.vue'
import { Menu } from 'lucide-vue-next'
import {
  LayoutDashboard,
  Plane,
  CalendarCheck,
  Star,
  FileCheck2,
} from 'lucide-vue-next'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const sessionStore = useSessionStore()

const mobileOpen = ref(false)

const userEmail = computed(() => sessionStore.user?.email || 'User')
const userInitials = computed(() => {
  const name = sessionStore.fullName
  if (!name) return 'U'
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
})

const currentPageTitle = computed(() => {
  return (route.meta.title as string) || 'Dashboard'
})

const navSections: NavSection[] = [
  {
    label: 'Main',
    items: [
      { name: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, to: '/client/dashboard' },
      { name: 'aircraft', label: 'Browse Aircraft', icon: Plane, to: '/client/aircraft' },
      { name: 'bookings', label: 'My Bookings', icon: CalendarCheck, to: '/client/bookings' },
      { name: 'reviews', label: 'My Reviews', icon: Star, to: '/client/reviews' },
    ],
  },
  {
    label: 'Account',
    items: [
      { name: 'licenses', label: 'My Licenses', icon: FileCheck2, to: '/client/licenses' },
    ],
  },
]

async function handleLogout(): Promise<void> {
  await authStore.logout()
  router.push({ name: 'login' })
}
</script>
