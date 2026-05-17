<template>
  <div>
    <button
      class="bg-transparent border-none cursor-pointer text-blue-600 text-base font-medium p-0 mb-6 transition-colors hover:text-blue-700 hover:underline"
      @click="$router.back()"
    >← Back</button>

    <div class="bg-white border border-slate-200 rounded-xl p-8 max-w-xl shadow-sm">
      <h1 class="text-2xl font-bold text-slate-900 mb-1 tracking-tight">Write a Review</h1>

      <LoadingSpinner v-if="loadingBooking" />
      <ErrorState v-else-if="loadError" :message="loadError" />

      <template v-else-if="booking">
        <p class="text-base text-slate-500 mb-6">
          Review for <strong>{{ booking.aircraftName }}</strong>
        </p>

        <AppAlert v-if="submitError" type="error" class="mb-4">{{ submitError }}</AppAlert>
        <AppAlert v-if="submitSuccess" type="success" class="mb-4">
          Your review has been submitted successfully!
        </AppAlert>

        <form v-if="!submitSuccess" @submit.prevent="handleSubmit" class="flex flex-col gap-5" novalidate>
          <!-- Rating -->
          <div class="flex flex-col gap-1">
            <label class="text-base font-medium text-slate-600">Rating <span class="text-red-500">*</span></label>
            <div class="flex gap-1">
              <button
                v-for="star in 5"
                :key="star"
                type="button"
                :class="[
                  'bg-transparent border-none text-[2rem] cursor-pointer p-0 transition-all',
                  star <= form.rating ? 'text-amber-400' : 'text-slate-200',
                  'hover:text-amber-400 hover:scale-110'
                ]"
                @click="form.rating = star"
                :title="`${star} star${star > 1 ? 's' : ''}`"
              >★</button>
            </div>
            <span v-if="errors.rating" class="text-sm text-red-500">{{ errors.rating }}</span>
          </div>

          <!-- Review type -->
          <div class="flex flex-col gap-1">
            <label class="text-base font-medium text-slate-600" for="review-type">Review type</label>
            <select
              id="review-type"
              v-model="form.reviewType"
              class="w-full px-3 py-2 border border-slate-200 rounded-lg text-base text-slate-900 bg-white transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/10"
            >
              <option value="">General</option>
              <option value="Safety">Safety</option>
              <option value="Comfort">Comfort</option>
              <option value="Performance">Performance</option>
              <option value="ValueForMoney">Value for Money</option>
            </select>
          </div>

          <!-- Comment -->
          <div class="flex flex-col gap-1">
            <label class="text-base font-medium text-slate-600" for="review-comment">Comment</label>
            <textarea
              id="review-comment"
              v-model="form.comment"
              class="w-full px-3 py-2 border border-slate-200 rounded-lg text-base text-slate-900 resize-y font-sans transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/10"
              rows="5"
              placeholder="Share your experience with this aircraft..."
            ></textarea>
          </div>

          <AppButton type="submit" block :loading="submitting">Submit review</AppButton>
        </form>

        <div v-else class="flex gap-3 flex-wrap">
          <AppButton @click="$router.push({ name: 'review-list' })">View my reviews</AppButton>
          <AppButton variant="secondary" @click="$router.push({ name: 'booking-list' })">Back to bookings</AppButton>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { bookingService, reviewService } from '@/api'
import type { BookingDto } from '@/types/booking'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const route = useRoute()
const router = useRouter()

const booking = ref<BookingDto | null>(null)
const loadingBooking = ref(false)
const loadError = ref('')

const form = ref({ rating: 5, comment: '', reviewType: '' })
const errors = ref({ rating: '' })
const submitting = ref(false)
const submitError = ref('')
const submitSuccess = ref(false)

async function loadBooking() {
  loadingBooking.value = true
  loadError.value = ''
  try {
    const bookingId = route.params.bookingId as string
    booking.value = await bookingService.getById(bookingId)
    // Verify the booking is completed
    if (booking.value.status !== 'Completed') {
      loadError.value = 'You can only review completed bookings.'
      booking.value = null
    }
  } catch (err: unknown) {
    loadError.value = err instanceof Error ? err.message : 'Failed to load booking'
  } finally {
    loadingBooking.value = false
  }
}

function validate(): boolean {
  errors.value.rating = ''
  if (!form.value.rating || form.value.rating < 1 || form.value.rating > 5) {
    errors.value.rating = 'Please select a rating between 1 and 5'
    return false
  }
  return true
}

async function handleSubmit() {
  if (!validate() || !booking.value) return
  submitting.value = true
  submitError.value = ''
  try {
    await reviewService.create({
      aircraftId: booking.value.aircraftId,
      bookingId: booking.value.id,
      rating: form.value.rating,
      comment: form.value.comment || undefined,
      reviewType: form.value.reviewType || undefined,
    })
    submitSuccess.value = true
  } catch (err: unknown) {
    submitError.value = err instanceof Error ? err.message : 'Failed to submit review'
  } finally {
    submitting.value = false
  }
}

onMounted(loadBooking)
</script>
