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
      <header class="h-14 bg-white/80 backdrop-blur-md border-b border-slate-200/80 flex items-center px-6 gap-4 sticky top-0 z-30">
        <!-- Mobile hamburger -->
        <button
          class="md:hidden flex items-center justify-center w-9 h-9 rounded-lg text-slate-500 hover:text-slate-700 hover:bg-slate-100 transition-all duration-200 border-none bg-transparent cursor-pointer"
          @click="mobileOpen = true"
        >
          <Menu class="w-5 h-5" />
        </button>

        <div class="flex-1 flex items-center gap-2">
          <span class="text-xs text-slate-400 uppercase tracking-wider font-medium">Company:</span>
          <span class="font-semibold text-slate-800">{{ companyName }}</span>
        </div>
        <RouterLink to="/admin/profile" class="flex items-center gap-3 no-underline p-1.5 px-2.5 rounded-xl transition-colors duration-200 hover:bg-slate-100">
          <span class="text-sm text-slate-600 font-medium hidden sm:inline">{{ userEmail }}</span>
          <div class="w-8 h-8 rounded-full bg-gradient-to-br from-amber-500 to-amber-600 text-white flex items-center justify-center text-[11px] font-bold ring-2 ring-amber-500/15">
            {{ userInitials }}
          </div>
        </RouterLink>
      </header>

      <main class="flex-1 p-6 overflow-y-auto">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/authStore'
import { useSessionStore } from '@/stores/sessionStore'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import type { NavSection } from '@/components/layout/AppSidebar.vue'
import { Menu } from 'lucide-vue-next'
import {
  LayoutDashboard,
  Plane,
  CalendarCheck,
  Wrench,
  Building2,
} from 'lucide-vue-next'

const router = useRouter()
const authStore = useAuthStore()
const sessionStore = useSessionStore()

const mobileOpen = ref(false)

const userEmail = computed(() => sessionStore.user?.email || 'Admin')
const companyName = computed(() => sessionStore.activeCompany?.companyName || '')
const userInitials = computed(() => {
  const name = sessionStore.fullName
  if (!name) return 'A'
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
})

const navSections: NavSection[] = [
  {
    label: 'Main',
    items: [
      { name: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, to: '/admin/dashboard' },
      { name: 'aircraft', label: 'Manage Aircraft', icon: Plane, to: '/admin/aircraft' },
      { name: 'bookings', label: 'Manage Bookings', icon: CalendarCheck, to: '/admin/bookings' },
      { name: 'maintenance', label: 'Maintenance', icon: Wrench, to: '/admin/maintenance' },
    ],
  },
  {
    label: 'Settings',
    items: [
      { name: 'settings', label: 'Company Settings', icon: Building2, to: '/admin/settings' },
    ],
  },
]

async function handleLogout(): Promise<void> {
  await authStore.logout()
  router.push({ name: 'login' })
}
</script>
