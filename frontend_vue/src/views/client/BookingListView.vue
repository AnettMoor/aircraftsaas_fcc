<template>
  <div>
    <h1 class="text-2xl font-bold text-slate-900 m-0 mb-6 tracking-tight">My Bookings</h1>

    <!-- Tabs -->
    <div class="flex border-b-2 border-slate-200 mb-6 overflow-x-auto" role="tablist">
      <button
        v-for="tab in tabs"
        :key="tab.value"
        :class="[
          'inline-flex items-center gap-2 px-5 py-3 border-none bg-transparent text-sm font-medium text-slate-500 cursor-pointer border-b-2 border-transparent -mb-[2px] transition-all duration-150 whitespace-nowrap',
          activeTab === tab.value ? '!text-blue-600 !border-b-blue-600 !font-semibold' : 'hover:text-slate-700'
        ]"
        @click="activeTab = tab.value"
        role="tab"
      >{{ tab.label }}</button>
    </div>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadBookings" />

    <EmptyState
      v-else-if="filtered.length === 0"
      icon="📋"
      title="No bookings"
      :description="activeTab === 'all' ? 'You have no bookings yet.' : `No ${activeTab} bookings.`"
    >
      <template #action>
        <AppButton @click="$router.push({ name: 'aircraft-list' })">Browse aircraft</AppButton>
      </template>
    </EmptyState>

    <div v-else class="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm">
      <div class="grid grid-cols-[1fr_1fr_1.5fr_1fr_auto] items-center px-5 py-3 gap-4 border-b border-slate-100 bg-slate-50 font-semibold text-xs text-slate-600 uppercase tracking-wider max-sm:hidden">
        <span>Reference</span>
        <span>Aircraft</span>
        <span>Dates</span>
        <span>Status</span>
        <span></span>
      </div>
      <div
        v-for="b in filtered"
        :key="b.id"
        class="grid grid-cols-[1fr_1fr_1.5fr_1fr_auto] items-center px-5 py-3 gap-4 border-b border-slate-100 last:border-b-0 text-sm transition-colors duration-150 hover:bg-slate-50 max-sm:grid-cols-[1fr_1fr]"
      >
        <span class="font-mono font-semibold text-xs text-slate-600">#{{ b.id.slice(0, 8) }}</span>
        <span>{{ b.aircraftName ?? '—' }}</span>
        <span class="text-xs text-slate-500 leading-relaxed">{{ fmt(b.startDateTime) }}<br>{{ fmt(b.endDateTime) }}</span>
        <span><span :class="badgeClasses(b.status)">{{ b.status }}</span></span>
        <span>
          <RouterLink :to="{ name: 'booking-detail', params: { id: b.id } }" class="text-sm text-blue-600 no-underline whitespace-nowrap font-medium transition-colors duration-150 hover:text-blue-700 hover:underline">
            View →
          </RouterLink>
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { bookingService } from '@/api'
import type { BookingDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import { badgeClasses } from '@/composables/useBadgeClasses'

type Tab = 'all' | 'Pending' | 'Approved' | 'Paid' | 'Completed' | 'Cancelled'

const tabs: { label: string; value: Tab }[] = [
  { label: 'All', value: 'all' },
  { label: 'Pending', value: 'Pending' },
  { label: 'Approved', value: 'Approved' },
  { label: 'Paid', value: 'Paid' },
  { label: 'Completed', value: 'Completed' },
  { label: 'Cancelled', value: 'Cancelled' },
]

const bookings = ref<BookingDto[]>([])
const loading = ref(false)
const error = ref('')
const activeTab = ref<Tab>('all')

const filtered = computed(() => {
  if (activeTab.value === 'all') return bookings.value
  if (activeTab.value === 'Pending') return bookings.value.filter((b) => b.status === 'Pending' || b.status === 'Requested')
  return bookings.value.filter((b) => b.status === activeTab.value)
})

function fmt(iso: string) {
  return new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

async function loadBookings() {
  loading.value = true
  error.value = ''
  try {
    bookings.value = await bookingService.getMy()
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load bookings'
  } finally {
    loading.value = false
  }
}

onMounted(loadBookings)
</script>
