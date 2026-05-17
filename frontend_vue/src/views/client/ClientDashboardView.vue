<template>
  <div>
    <h1 class="text-2xl font-bold text-slate-900 m-0 mb-6 tracking-tight">Dashboard</h1>

    <!-- Stats -->
    <div class="grid grid-cols-4 gap-4 mb-8 max-sm:grid-cols-1">
      <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-t-2 border-t-blue-600">
        <p class="text-xs font-medium text-slate-500 m-0 mb-2">My Bookings</p>
        <p class="text-3xl font-bold text-slate-900 m-0 tracking-tight">{{ bookings.length }}</p>
      </div>
      <div class="bg-gradient-to-br from-blue-600 to-blue-700 border border-blue-700 rounded-xl px-6 py-5 shadow-[0_2px_8px_rgba(29,78,216,0.3)] transition-all duration-200 hover:shadow-[0_4px_16px_rgba(29,78,216,0.4)] hover:-translate-y-px">
        <p class="text-xs font-medium text-blue-200 m-0 mb-2">Pending</p>
        <p class="text-3xl font-bold text-white m-0 tracking-tight">{{ pendingCount }}</p>
      </div>
      <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card transition-all duration-200 hover:shadow-card-hover hover:-translate-y-px border-t-2 border-t-emerald-500">
        <p class="text-xs font-medium text-slate-500 m-0 mb-2">Confirmed</p>
        <p class="text-3xl font-bold text-emerald-600 m-0 tracking-tight">{{ confirmedCount }}</p>
      </div>
      <div :class="['bg-white border rounded-xl px-6 py-5 shadow-sm transition-all duration-150 hover:shadow-md hover:-translate-y-px', expiringLicenses.length > 0 ? 'border-l-4 border-l-amber-500 border-slate-200' : 'border-slate-200']">
        <p class="text-xs font-medium text-slate-500 m-0 mb-2">Licenses</p>
        <p class="text-3xl font-bold text-slate-900 m-0 tracking-tight">{{ licenses.length }}</p>
      </div>
    </div>

    <!-- License expiry warnings -->
    <div v-if="expiringLicenses.length > 0" class="mb-6">
      <div class="flex items-start gap-3 bg-amber-50 border border-amber-300 rounded-xl px-5 py-4">
        <span class="text-xl flex-shrink-0">⚠</span>
        <div class="flex-1 text-sm text-amber-800">
          <strong class="block mb-1">License expiry alert</strong>
          <p v-for="lic in expiringLicenses" :key="lic.id" class="text-xs my-0.5">
            {{ lic.licenseType }} ({{ lic.licenseNumber }}) —
            {{ new Date(lic.expiryDate) < new Date() ? 'Expired' : 'Expires' }}
            {{ formatDate(lic.expiryDate) }}
          </p>
        </div>
        <RouterLink :to="{ name: 'license-list' }" class="text-xs text-amber-800 underline whitespace-nowrap self-center">View licenses →</RouterLink>
      </div>
    </div>

    <!-- Recent bookings -->
    <section class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-card">
      <div class="flex justify-between items-center mb-4">
        <h2 class="text-base font-semibold text-slate-900 m-0">Recent bookings</h2>
        <RouterLink :to="{ name: 'booking-list' }" class="text-xs text-blue-600 no-underline font-medium transition-colors duration-150 hover:text-blue-700 hover:underline">View all →</RouterLink>
      </div>

      <LoadingSpinner v-if="loading" />

      <ErrorState
        v-else-if="error"
        :message="error"
        retryable
        @retry="loadData"
      />

      <EmptyState
        v-else-if="bookings.length === 0"
        icon="✈"
        title="No bookings yet"
        description="Browse available aircraft and make your first booking."
      >
        <template #action>
          <AppButton @click="$router.push({ name: 'aircraft-list' })">Browse aircraft</AppButton>
        </template>
      </EmptyState>

      <div v-else class="flex flex-col gap-2">
        <div
          v-for="booking in bookings.slice(0, 5)"
          :key="booking.id"
          class="flex justify-between items-center px-4 py-3 bg-slate-50 rounded-lg transition-colors duration-150 hover:bg-slate-100"
        >
          <div class="flex flex-col gap-px">
            <span class="text-xs font-semibold text-slate-600 font-mono">#{{ booking.id.slice(0, 8) }}</span>
            <span class="text-xs text-slate-500">
              {{ formatDate(booking.startDateTime) }} — {{ formatDate(booking.endDateTime) }}
            </span>
          </div>
          <span :class="badgeClasses(booking.status)">
            {{ booking.status }}
          </span>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { bookingService, licenseService } from '@/api'
import type { BookingDto, LicenseDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'

const bookings = ref<BookingDto[]>([])
const licenses = ref<LicenseDto[]>([])
const loading = ref(false)
const error = ref('')

const pendingCount = computed(() => bookings.value.filter((b) => b.status === 'Pending').length)
const confirmedCount = computed(() => bookings.value.filter((b) => b.status === 'Approved').length)
const expiringLicenses = computed(() =>
  licenses.value.filter((l) => {
    const expiry = new Date(l.expiryDate)
    const now = new Date()
    const thirtyDays = 30 * 24 * 60 * 60 * 1000
    return expiry.getTime() - now.getTime() < thirtyDays
  })
)

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

const badgeMap: Record<string, string> = {
  approved: 'bg-emerald-100 text-emerald-800',
  available: 'bg-emerald-100 text-emerald-800',
  success: 'bg-emerald-100 text-emerald-800',
  valid: 'bg-emerald-100 text-emerald-800',
  ok: 'bg-emerald-100 text-emerald-800',
  pending: 'bg-amber-100 text-amber-800',
  requested: 'bg-amber-100 text-amber-800',
  cancelled: 'bg-red-100 text-red-800',
  rejected: 'bg-red-100 text-red-800',
  expired: 'bg-red-100 text-red-800',
  paid: 'bg-blue-100 text-blue-800',
  completed: 'bg-blue-100 text-blue-800',
  maintenance: 'bg-blue-100 text-blue-800',
}

function badgeClasses(status: string) {
  const base = 'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium whitespace-nowrap'
  return `${base} ${badgeMap[status.toLowerCase()] || 'bg-slate-100 text-slate-600'}`
}

async function loadData() {
  loading.value = true
  error.value = ''
  try {
    const [bookingData, licenseData] = await Promise.all([
      bookingService.getMy(),
      licenseService.getAll().catch(() => []),
    ])
    bookings.value = bookingData
    licenses.value = licenseData
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load dashboard data'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>
