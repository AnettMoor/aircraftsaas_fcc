<template>
  <div>
    <h1 class="text-2xl font-bold text-slate-900 mb-1 tracking-tight">Company Dashboard</h1>
    <p v-if="sessionStore.activeCompany" class="text-base text-slate-500 mb-6">
      {{ sessionStore.activeCompany.companyName }}
    </p>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadData" />

    <template v-else>
      <div class="grid grid-cols-2 md:grid-cols-5 gap-4 mb-8">
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-t-2 border-t-blue-600">
          <p class="text-sm text-slate-500 font-medium mb-2">Aircraft</p>
          <p class="text-3xl font-bold text-slate-900 tracking-tight">{{ stats.aircraft }}</p>
        </div>
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-t-2 border-t-blue-600">
          <p class="text-sm text-slate-500 font-medium mb-2">Total bookings</p>
          <p class="text-3xl font-bold text-slate-900 tracking-tight">{{ stats.bookings }}</p>
        </div>
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-l-4 border-l-amber-400">
          <p class="text-sm text-slate-500 font-medium mb-2">Pending</p>
          <p class="text-3xl font-bold text-amber-600 tracking-tight">{{ stats.pending }}</p>
        </div>
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-l-4 border-l-emerald-500">
          <p class="text-sm text-slate-500 font-medium mb-2">Confirmed</p>
          <p class="text-3xl font-bold text-emerald-600 tracking-tight">{{ stats.confirmed }}</p>
        </div>
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-t-2 border-t-blue-600">
          <p class="text-sm text-slate-500 font-medium mb-2">Maintenance</p>
          <p class="text-3xl font-bold text-slate-900 tracking-tight">{{ stats.maintenance }}</p>
        </div>
      </div>

      <div class="flex gap-4 flex-wrap">
        <RouterLink
          :to="{ name: 'admin-aircraft' }"
          class="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-5 py-4 no-underline text-slate-900 font-medium text-base shadow-card transition-all duration-200 hover:shadow-card-hover hover:border-blue-600/20 hover:-translate-y-px flex-1 min-w-[12rem] group"
        >
          <span class="text-2xl">✈</span>
          <span class="group-hover:text-blue-700 transition-colors">Manage aircraft</span>
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-bookings' }"
          class="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-5 py-4 no-underline text-slate-900 font-medium text-base shadow-card transition-all duration-200 hover:shadow-card-hover hover:border-blue-600/20 hover:-translate-y-px flex-1 min-w-[12rem] group"
        >
          <span class="text-2xl">📋</span>
          <span class="group-hover:text-blue-700 transition-colors">Manage bookings</span>
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-maintenance' }"
          class="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-5 py-4 no-underline text-slate-900 font-medium text-base shadow-card transition-all duration-200 hover:shadow-card-hover hover:border-blue-600/20 hover:-translate-y-px flex-1 min-w-[12rem] group"
        >
          <span class="text-2xl">🔧</span>
          <span class="group-hover:text-blue-700 transition-colors">Maintenance records</span>
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-settings' }"
          class="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-5 py-4 no-underline text-slate-900 font-medium text-base shadow-card transition-all duration-200 hover:shadow-card-hover hover:border-blue-600/20 hover:-translate-y-px flex-1 min-w-[12rem] group"
        >
          <span class="text-2xl">⚙</span>
          <span class="group-hover:text-blue-700 transition-colors">Company settings</span>
        </RouterLink>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useSessionStore } from '@/stores/sessionStore'
import { aircraftService, bookingService, maintenanceService } from '@/api'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'

const sessionStore = useSessionStore()

const loading = ref(false)
const error = ref('')
const stats = reactive({ aircraft: 0, bookings: 0, pending: 0, confirmed: 0, maintenance: 0 })

async function loadData() {
  loading.value = true
  error.value = ''
  try {
    const [aircraft, bookings, maintenance] = await Promise.all([
      aircraftService.getCompanyAircraft(),
      bookingService.getAll(),
      maintenanceService.getAll().catch(() => []),
    ])
    stats.aircraft = aircraft.length
    stats.bookings = bookings.length
    stats.pending = bookings.filter((b) => b.status === 'Pending').length
    stats.confirmed = bookings.filter((b) => b.status === 'Approved').length
    stats.maintenance = maintenance.length
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load dashboard data'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>
