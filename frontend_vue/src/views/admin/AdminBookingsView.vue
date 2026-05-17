<template>
  <div>
    <h1 class="text-2xl font-bold tracking-tight text-slate-900 mb-6">Bookings</h1>

    <!-- Tabs -->
    <div class="flex gap-1 border-b border-slate-200 mb-6 overflow-x-auto">
      <button
        v-for="tab in tabs"
        :key="tab.value"
        :class="[
          'px-4 py-2 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
          activeTab === tab.value
            ? 'border-blue-600 text-blue-600'
            : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
        ]"
        @click="activeTab = tab.value"
      >
        {{ tab.label }}
        <span :class="['ml-1.5 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-semibold', tab.badgeClass]">{{ tab.count }}</span>
      </button>
    </div>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadBookings" />

    <EmptyState v-else-if="filtered.length === 0" icon="📋" title="No bookings" :description="`No ${activeTab === 'all' ? '' : activeTab.toLowerCase() + ' '}bookings found.`" />

    <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-slate-200 bg-slate-50">
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Ref</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Aircraft</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Booked by</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Start</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">End</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Status</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Actions</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          <tr
            v-for="b in filtered"
            :key="b.id"
            class="cursor-pointer hover:bg-slate-50 transition-colors"
            @click="router.push({ name: 'admin-booking-detail', params: { id: b.id } })"
          >
            <td class="px-4 py-3 font-mono text-xs text-slate-700">#{{ b.id.slice(0, 8) }}</td>
            <td class="px-4 py-3 text-slate-700">{{ b.aircraftName ?? '—' }}</td>
            <td class="px-4 py-3 text-slate-700">{{ b.pilotName ?? '—' }}</td>
            <td class="px-4 py-3 text-slate-700">{{ fmt(b.startDateTime) }}</td>
            <td class="px-4 py-3 text-slate-700">{{ fmt(b.endDateTime) }}</td>
            <td class="px-4 py-3">
              <span :class="statusBadge(b.status)">{{ b.status }}</span>
            </td>
            <td class="px-4 py-3" @click.stop>
              <div class="flex gap-2 flex-wrap">
                <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="router.push({ name: 'admin-booking-detail', params: { id: b.id } })">View</button>
                <button v-if="b.status === 'Requested' || b.status === 'Pending'" class="text-sm font-medium text-emerald-600 hover:text-emerald-700 transition-colors" @click="approve(b.id)" :disabled="acting === b.id">Approve</button>
                <button v-if="b.status === 'Requested' || b.status === 'Pending'" class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="reject(b.id)" :disabled="acting === b.id">Reject</button>
                <button v-if="b.status === 'Approved'" class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="cancel(b.id)" :disabled="acting === b.id">Cancel Booking</button>
                <button v-if="b.status === 'Paid'" class="text-sm font-medium text-emerald-600 hover:text-emerald-700 transition-colors" @click="complete(b.id)" :disabled="acting === b.id">Complete</button>
                <button v-if="b.status === 'Paid'" class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="cancel(b.id)" :disabled="acting === b.id">Cancel Booking</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { bookingService } from '@/api'
import type { BookingDto } from '@/types'
import { badgeClasses } from '@/composables/useBadgeClasses'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'

const router = useRouter()

type Tab = 'all' | 'Requested' | 'Pending' | 'Approved' | 'Paid' | 'Completed' | 'Cancelled'

const bookings = ref<BookingDto[]>([])
const loading = ref(false)
const error = ref('')
const acting = ref<string | null>(null)
const activeTab = ref<Tab>('all')

function statusBadge(status: string): string {
  return badgeClasses(status)
}

function countByStatus(status: string) {
  return bookings.value.filter((b) => b.status === status).length
}

const tabBadgeMap: Record<string, string> = {
  secondary: 'bg-slate-100 text-slate-600',
  warning: 'bg-amber-100 text-amber-700',
  success: 'bg-emerald-100 text-emerald-700',
  danger: 'bg-red-100 text-red-700',
}

const tabs = computed(() => [
  { label: 'All', value: 'all' as Tab, count: bookings.value.length, badgeClass: tabBadgeMap.secondary },
  { label: 'Requested', value: 'Requested' as Tab, count: countByStatus('Requested'), badgeClass: tabBadgeMap.warning },
  { label: 'Approved', value: 'Approved' as Tab, count: countByStatus('Approved'), badgeClass: tabBadgeMap.success },
  { label: 'Paid', value: 'Paid' as Tab, count: countByStatus('Paid'), badgeClass: tabBadgeMap.success },
  { label: 'Completed', value: 'Completed' as Tab, count: countByStatus('Completed'), badgeClass: tabBadgeMap.secondary },
  { label: 'Cancelled', value: 'Cancelled' as Tab, count: countByStatus('Cancelled'), badgeClass: tabBadgeMap.danger },
])

const filtered = computed(() =>
  activeTab.value === 'all' ? bookings.value : bookings.value.filter((b) => b.status === activeTab.value)
)

function fmt(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

async function loadBookings() {
  loading.value = true
  error.value = ''
  try {
    bookings.value = await bookingService.getAll()
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load bookings'
  } finally {
    loading.value = false
  }
}

async function approve(id: string) {
  acting.value = id
  try {
    await bookingService.approve(id)
    const b = bookings.value.find((x) => x.id === id)
    if (b) b.status = 'Approved'
  } finally { acting.value = null }
}

async function reject(id: string) {
  acting.value = id
  try {
    await bookingService.reject(id, '')
    const b = bookings.value.find((x) => x.id === id)
    if (b) b.status = 'Rejected'
  } finally { acting.value = null }
}

async function cancel(id: string) {
  acting.value = id
  try {
    await bookingService.cancel(id)
    const b = bookings.value.find((x) => x.id === id)
    if (b) b.status = 'Cancelled'
  } finally { acting.value = null }
}

async function complete(id: string) {
  acting.value = id
  try {
    await bookingService.complete(id)
    const b = bookings.value.find((x) => x.id === id)
    if (b) b.status = 'Completed'
  } finally { acting.value = null }
}

onMounted(loadBookings)
</script>
