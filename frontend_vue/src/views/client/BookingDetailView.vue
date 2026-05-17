<template>
  <div>
    <button class="bg-transparent border-none cursor-pointer text-blue-600 text-sm font-medium p-0 mb-6 transition-colors duration-150 hover:text-blue-700 hover:underline" @click="$router.back()">← Back to bookings</button>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadBooking" />

    <template v-else-if="booking">
      <div class="flex justify-between items-start mb-6 gap-4 flex-wrap">
        <div>
          <p class="text-xs text-slate-500 font-mono font-semibold m-0 mb-1">#{{ booking.id.slice(0, 8) }}</p>
          <h1 class="text-2xl font-bold text-slate-900 m-0 tracking-tight">{{ booking.aircraftName ?? 'Aircraft' }}</h1>
        </div>
        <span :class="badgeClasses(booking.status)">{{ booking.status }}</span>
      </div>

      <div class="grid grid-cols-2 gap-4 max-sm:grid-cols-1">
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-base font-semibold text-slate-900 m-0 mb-4">Booking details</h2>
          <dl class="m-0">
            <div class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Start</dt><dd class="text-slate-900 m-0">{{ fmt(booking.startDateTime) }}</dd></div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">End</dt><dd class="text-slate-900 m-0">{{ fmt(booking.endDateTime) }}</dd></div>
            <div v-if="booking.totalAmount" class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Total price</dt><dd class="text-slate-900 m-0">€{{ booking.totalAmount.toFixed(2) }}</dd></div>
            <div v-if="booking.purpose" class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Purpose</dt><dd class="text-slate-900 m-0">{{ booking.purpose }}</dd></div>
            <div v-if="booking.rejectionReason" class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Rejection reason</dt><dd class="text-slate-900 m-0">{{ booking.rejectionReason }}</dd></div>
          </dl>
        </div>

        <!-- Payment card for approved bookings -->
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm" v-if="canPay">
          <h2 class="text-base font-semibold text-slate-900 m-0 mb-4">💳 Pay for Booking</h2>
          <AppAlert v-if="paymentError" type="error" class="mb-3">{{ paymentError }}</AppAlert>
          <AppAlert v-if="paymentSuccess" type="success" class="mb-3">Payment submitted successfully!</AppAlert>

          <div class="flex flex-col gap-4" v-if="!paymentSuccess">
            <div class="flex flex-col gap-1">
              <label for="paymentMethod" class="text-xs font-medium text-slate-700">Payment method</label>
              <select id="paymentMethod" v-model="paymentMethod" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm bg-white outline-none transition-all duration-150 focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15">
                <option value="">-- Select a payment method --</option>
                <option value="CreditCard">Credit Card</option>
                <option value="DebitCard">Debit Card</option>
                <option value="BankTransfer">Bank Transfer</option>
                <option value="PayPal">PayPal</option>
              </select>
            </div>

            <div class="flex flex-col gap-1">
              <label for="paymentDetails" class="text-xs font-medium text-slate-700">Payment details (optional)</label>
              <textarea
                id="paymentDetails"
                v-model="paymentDetails"
                rows="2"
                class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm resize-y outline-none transition-all duration-150 focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15"
                placeholder="Any additional payment details…"
              ></textarea>
            </div>

            <div class="text-base text-slate-900 py-3 border-t border-slate-100">
              <strong>Total to pay:</strong> €{{ booking.totalAmount?.toFixed(2) ?? '0.00' }}
            </div>

            <AppButton
              variant="primary"
              :loading="paying"
              :disabled="!paymentMethod"
              @click="submitPayment"
            >💳 Pay now</AppButton>
          </div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm" v-if="canEdit || canCancel || canReview">
          <h2 class="text-base font-semibold text-slate-900 m-0 mb-4">Actions</h2>
          <AppAlert v-if="actionError" type="error" class="mb-3">{{ actionError }}</AppAlert>
          <div class="flex flex-col gap-3">
            <AppButton
              v-if="canEdit"
              @click="$router.push({ name: 'booking-edit', params: { id: booking.id } })"
            >✏️ Edit booking</AppButton>
            <AppButton
              v-if="canCancel"
              variant="danger"
              :loading="acting"
              @click="cancel"
            >Cancel booking</AppButton>
            <AppButton
              v-if="canReview"
              @click="$router.push({ name: 'review-create', params: { bookingId: booking.id } })"
            >⭐ Write a review</AppButton>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { bookingService, reviewService } from '@/api'
import type { BookingDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import { useSessionStore } from '@/stores/sessionStore'
import { badgeClasses } from '@/composables/useBadgeClasses'

const route = useRoute()
const sessionStore = useSessionStore()
const booking = ref<BookingDto | null>(null)
const loading = ref(false)
const error = ref('')
const acting = ref(false)
const actionError = ref('')
const hasExistingReview = ref(false)
const paying = ref(false)
const paymentError = ref('')
const paymentSuccess = ref(false)
const paymentMethod = ref('')
const paymentDetails = ref('')

const canEdit = computed(() =>
  booking.value != null && ['Pending', 'Requested'].includes(booking.value.status)
)

const canCancel = computed(() =>
  booking.value && ['Pending', 'Requested', 'Approved'].includes(booking.value.status)
)

const canPay = computed(() =>
  booking.value?.status === 'Approved'
)

const canReview = computed(() =>
  booking.value?.status === 'Completed' && !hasExistingReview.value
)

function fmt(iso: string) {
  return new Date(iso).toLocaleString('en-GB', { dateStyle: 'long', timeStyle: 'short' })
}

async function loadBooking() {
  loading.value = true
  error.value = ''
  try {
    booking.value = await bookingService.getById(route.params.id as string)
    if (booking.value.status === 'Completed') {
      try {
        const reviews = await reviewService.getByAircraft(booking.value.aircraftId)
        const userId = sessionStore.user?.id
        hasExistingReview.value = reviews.some(
          r => r.bookingId === booking.value!.id && r.authorId === userId
        )
      } catch {
        hasExistingReview.value = false
      }
    }
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load booking'
  } finally {
    loading.value = false
  }
}

async function cancel() {
  if (!booking.value) return
  acting.value = true
  actionError.value = ''
  try {
    await bookingService.cancel(booking.value.id)
    booking.value = { ...booking.value, status: 'Cancelled' }
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : 'Failed to cancel'
  } finally {
    acting.value = false
  }
}

async function submitPayment() {
  if (!booking.value || !paymentMethod.value) return
  paying.value = true
  paymentError.value = ''
  paymentSuccess.value = false
  try {
    booking.value = await bookingService.pay(booking.value.id, {
      paymentMethod: paymentMethod.value,
      paymentDetails: paymentDetails.value || undefined,
    })
    paymentSuccess.value = true
  } catch (err: unknown) {
    paymentError.value = err instanceof Error ? err.message : 'Payment failed'
  } finally {
    paying.value = false
  }
}

onMounted(loadBooking)
</script>
