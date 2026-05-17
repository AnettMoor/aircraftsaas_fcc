<template>
  <div>
    <button
      class="bg-transparent border-none cursor-pointer text-blue-600 text-base font-medium p-0 mb-6 transition-colors hover:text-blue-700 hover:underline"
      @click="$router.push({ name: 'admin-bookings' })"
    >← Back to bookings</button>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadBooking" />

    <template v-else-if="booking">
      <div class="flex justify-between items-start mb-6 gap-4 flex-wrap">
        <div>
          <p class="text-sm text-slate-500 font-mono font-semibold mb-1">#{{ booking.id.slice(0, 8) }}</p>
          <h1 class="text-2xl font-bold text-slate-900 tracking-tight">{{ booking.aircraftName ?? 'Aircraft' }}</h1>
        </div>
        <span :class="statusBadge(booking.status)">{{ booking.status }}</span>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <!-- Booking info card -->
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">Booking Details</h2>
          <dl>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Status</dt>
              <dd class="text-slate-900 m-0 break-words"><span :class="statusBadge(booking.status)">{{ booking.status }}</span></dd>
            </div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Start</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.startDateTime) }}</dd>
            </div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">End</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.endDateTime) }}</dd>
            </div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Duration</dt>
              <dd class="text-slate-900 m-0 break-words">{{ duration }}</dd>
            </div>
            <div v-if="booking.totalAmount" class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Total price</dt>
              <dd class="text-slate-900 m-0 break-words">€{{ booking.totalAmount.toFixed(2) }}</dd>
            </div>
            <div v-if="booking.purpose" class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Purpose / Comments</dt>
              <dd class="text-slate-900 m-0 break-words">{{ booking.purpose }}</dd>
            </div>
            <div class="flex gap-4 py-2 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Created</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.createdAt) }}</dd>
            </div>
          </dl>
        </div>

        <!-- Pilot / booker info card -->
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">Booked By</h2>
          <dl>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Pilot name</dt>
              <dd class="text-slate-900 m-0 break-words">{{ booking.pilotName ?? '—' }}</dd>
            </div>
            <div class="flex gap-4 py-2 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Pilot ID</dt>
              <dd class="text-slate-900 m-0 break-words font-mono text-sm">{{ booking.pilotId }}</dd>
            </div>
          </dl>
        </div>

        <!-- Status timeline card -->
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">Status Timeline</h2>
          <dl>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Created</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.createdAt) }}</dd>
            </div>
            <div v-if="booking.approvedAt" class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Approved</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.approvedAt) }}</dd>
            </div>
            <div v-if="booking.paidAt" class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Paid</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.paidAt) }}</dd>
            </div>
            <div v-if="booking.completedAt" class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Completed</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.completedAt) }}</dd>
            </div>
            <div v-if="booking.cancelledAt" class="flex gap-4 py-2 border-b border-slate-100 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Cancelled</dt>
              <dd class="text-slate-900 m-0 break-words">{{ fmtDateTime(booking.cancelledAt) }}</dd>
            </div>
            <div v-if="booking.rejectionReason" class="flex gap-4 py-2 text-base">
              <dt class="w-36 text-slate-500 font-medium shrink-0">Rejection reason</dt>
              <dd class="text-slate-900 m-0 break-words">{{ booking.rejectionReason }}</dd>
            </div>
          </dl>
        </div>

        <!-- Actions card -->
        <div v-if="canApprove || canReject || canComplete || canCancel" class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">Actions</h2>
          <AppAlert v-if="actionError" type="error" class="mb-4">{{ actionError }}</AppAlert>

          <!-- Rejection reason input -->
          <div v-if="showRejectInput" class="mb-4">
            <label for="reject-reason" class="block text-sm font-medium text-slate-700 mb-1">Rejection reason (optional)</label>
            <textarea
              id="reject-reason"
              v-model="rejectReason"
              rows="3"
              class="w-full border border-slate-300 rounded-lg px-3 py-2 text-base font-sans resize-y transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15"
              placeholder="Enter a reason for rejection…"
            ></textarea>
          </div>

          <div class="flex flex-wrap gap-3">
            <button v-if="canApprove" class="text-sm font-medium text-emerald-600 hover:text-emerald-700 transition-colors" :disabled="acting" @click="approve">
              ✅ Approve
            </button>
            <button v-if="canReject && !showRejectInput" class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" :disabled="acting" @click="showRejectInput = true">
              ❌ Reject
            </button>
            <button v-if="showRejectInput" class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" :disabled="acting" @click="reject">
              Confirm Rejection
            </button>
            <button v-if="showRejectInput" class="text-sm font-medium text-slate-600 hover:text-slate-700 transition-colors" @click="showRejectInput = false; rejectReason = ''">
              Cancel
            </button>
            <button v-if="canComplete" class="text-sm font-medium text-emerald-600 hover:text-emerald-700 transition-colors" :disabled="acting" @click="complete">
              ✔️ Mark Complete
            </button>
            <button v-if="canCancel" class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" :disabled="acting" @click="cancel">
              🚫 Cancel Booking
            </button>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { bookingService } from '@/api'
import type { BookingDto } from '@/types'
import { badgeClasses } from '@/composables/useBadgeClasses'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const route = useRoute()
const booking = ref<BookingDto | null>(null)
const loading = ref(false)
const error = ref('')
const acting = ref(false)
const actionError = ref('')
const showRejectInput = ref(false)
const rejectReason = ref('')

function statusBadge(status: string): string {
  return badgeClasses(status)
}

const canApprove = computed(() => booking.value?.status === 'Requested' || booking.value?.status === 'Pending')
const canReject = computed(() => booking.value?.status === 'Requested' || booking.value?.status === 'Pending')
const canComplete = computed(() => booking.value?.status === 'Paid' || booking.value?.status === 'Approved')
const canCancel = computed(() =>
  booking.value != null && ['Requested', 'Pending', 'Approved', 'Paid'].includes(booking.value.status)
)

const duration = computed(() => {
  if (!booking.value) return '—'
  const start = new Date(booking.value.startDateTime)
  const end = new Date(booking.value.endDateTime)
  const diffMs = end.getTime() - start.getTime()
  const hours = Math.floor(diffMs / (1000 * 60 * 60))
  const minutes = Math.round((diffMs % (1000 * 60 * 60)) / (1000 * 60))
  if (hours < 24) return `${hours}h ${minutes}m`
  const days = Math.floor(hours / 24)
  const remHours = hours % 24
  return `${days}d ${remHours}h`
})

function fmtDateTime(iso: string) {
  return new Date(iso).toLocaleString('en-GB', { dateStyle: 'long', timeStyle: 'short' })
}

async function loadBooking() {
  loading.value = true
  error.value = ''
  try {
    booking.value = await bookingService.getById(route.params.id as string)
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load booking'
  } finally {
    loading.value = false
  }
}

async function approve() {
  if (!booking.value) return
  acting.value = true
  actionError.value = ''
  try {
    booking.value = await bookingService.approve(booking.value.id)
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : 'Failed to approve booking'
  } finally {
    acting.value = false
  }
}

async function reject() {
  if (!booking.value) return
  acting.value = true
  actionError.value = ''
  try {
    const updated = await bookingService.reject(booking.value.id, rejectReason.value)
    booking.value = updated
    showRejectInput.value = false
    rejectReason.value = ''
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : 'Failed to reject booking'
  } finally {
    acting.value = false
  }
}

async function complete() {
  if (!booking.value) return
  acting.value = true
  actionError.value = ''
  try {
    booking.value = await bookingService.complete(booking.value.id)
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : 'Failed to complete booking'
  } finally {
    acting.value = false
  }
}

async function cancel() {
  if (!booking.value) return
  acting.value = true
  actionError.value = ''
  try {
    booking.value = await bookingService.cancel(booking.value.id)
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : 'Failed to cancel booking'
  } finally {
    acting.value = false
  }
}

onMounted(loadBooking)
</script>
