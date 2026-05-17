<template>
  <div>
    <button class="bg-transparent border-none cursor-pointer text-blue-600 text-sm font-medium p-0 mb-6 transition-colors duration-150 hover:text-blue-700 hover:underline" @click="$router.back()">← Back to booking</button>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="loadError" :message="loadError" retryable @retry="loadBooking" />

    <template v-else-if="booking">
      <div class="flex justify-between items-start mb-6 gap-4 flex-wrap">
        <div>
          <p class="text-xs text-slate-500 font-mono font-semibold m-0 mb-1">#{{ booking.id.slice(0, 8) }}</p>
          <h1 class="text-2xl font-bold text-slate-900 m-0 mb-1 tracking-tight">Edit Booking</h1>
          <p class="text-base text-slate-600 m-0">{{ booking.aircraftName }}</p>
        </div>
        <span :class="badgeClasses(booking.status)">{{ booking.status }}</span>
      </div>

      <div class="grid grid-cols-2 gap-4 max-sm:grid-cols-1">
        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-base font-semibold text-slate-900 m-0 mb-4">Update booking details</h2>

          <AppAlert v-if="!canEdit" type="warning" class="mb-3">
            Only bookings with status Pending or Requested can be edited.
          </AppAlert>

          <AvailabilityCalendar
            v-if="booking.aircraftId"
            :aircraft-id="booking.aircraftId"
            :selected-start="form.startDateTime"
            :selected-end="form.endDateTime"
            :exclude-booking-start="originalBookingStart"
            :exclude-booking-end="originalBookingEnd"
            @select-date="onCalendarDateSelect"
          />

          <AppAlert v-if="saveError" type="error" class="mb-3">{{ saveError }}</AppAlert>
          <AppAlert v-if="saveSuccess" type="success" class="mb-3">
            Booking updated successfully!
          </AppAlert>

          <form @submit.prevent="submitUpdate" novalidate class="flex flex-col gap-4">
            <AppInput
              v-model="form.startDateTime"
              label="Start date & time"
              type="datetime-local"
              required
              :error="errors.startDateTime"
              :disabled="!canEdit"
            />
            <AppInput
              v-model="form.endDateTime"
              label="End date & time"
              type="datetime-local"
              required
              :error="errors.endDateTime"
              :disabled="!canEdit"
            />
            <AppInput
              v-model="form.purpose"
              label="Purpose / notes"
              placeholder="e.g. Training flight to Helsinki"
              :disabled="!canEdit"
            />

            <div class="flex gap-3 mt-2">
              <AppButton
                type="submit"
                :loading="saving"
                :disabled="!canEdit"
              >Save changes</AppButton>
              <AppButton
                variant="secondary"
                @click="$router.push({ name: 'booking-detail', params: { id: booking.id } })"
              >Cancel</AppButton>
            </div>
          </form>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-base font-semibold text-slate-900 m-0 mb-4">Current booking info</h2>
          <dl class="m-0">
            <div class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Aircraft</dt><dd class="text-slate-900 m-0">{{ booking.aircraftName }}</dd></div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Status</dt><dd class="text-slate-900 m-0">{{ booking.status }}</dd></div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Total price</dt><dd class="text-slate-900 m-0">€{{ booking.totalAmount.toFixed(2) }}</dd></div>
            <div class="flex gap-4 py-2 border-b border-slate-100 text-sm last:border-b-0"><dt class="w-28 text-slate-500 font-medium flex-shrink-0">Created</dt><dd class="text-slate-900 m-0">{{ fmt(booking.createdAt) }}</dd></div>
          </dl>
          <p class="text-xs text-slate-500 mt-4 mb-0">
            The total price will be recalculated automatically based on the new time range.
          </p>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { bookingService } from '@/api'
import type { BookingDto, UpdateBookingDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import AvailabilityCalendar from '@/components/common/AvailabilityCalendar.vue'
import { badgeClasses } from '@/composables/useBadgeClasses'

const route = useRoute()
const router = useRouter()

const booking = ref<BookingDto | null>(null)
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')
const saveSuccess = ref(false)

/** Original booking dates — used to exclude current booking from calendar "Booked" display */
const originalBookingStart = ref('')
const originalBookingEnd = ref('')

const form = ref({
  startDateTime: '',
  endDateTime: '',
  purpose: '',
})

const errors = ref({
  startDateTime: '',
  endDateTime: '',
})

const canEdit = computed(() =>
  booking.value != null && ['Pending', 'Requested'].includes(booking.value.status)
)

function fmt(iso: string) {
  return new Date(iso).toLocaleString('en-GB', { dateStyle: 'long', timeStyle: 'short' })
}

function toLocalInput(iso: string): string {
  const d = new Date(iso)
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function loadBooking() {
  loading.value = true
  loadError.value = ''
  try {
    booking.value = await bookingService.getById(route.params.id as string)
    originalBookingStart.value = booking.value.startDateTime
    originalBookingEnd.value = booking.value.endDateTime
    form.value.startDateTime = toLocalInput(booking.value.startDateTime)
    form.value.endDateTime = toLocalInput(booking.value.endDateTime)
    form.value.purpose = booking.value.purpose ?? ''
  } catch (err: unknown) {
    loadError.value = err instanceof Error ? err.message : 'Failed to load booking'
  } finally {
    loading.value = false
  }
}

function validate(): boolean {
  errors.value = { startDateTime: '', endDateTime: '' }
  let valid = true

  if (!form.value.startDateTime) {
    errors.value.startDateTime = 'Start date & time is required'
    valid = false
  }
  if (!form.value.endDateTime) {
    errors.value.endDateTime = 'End date & time is required'
    valid = false
  }
  if (form.value.startDateTime && form.value.endDateTime) {
    const start = new Date(form.value.startDateTime)
    const end = new Date(form.value.endDateTime)
    if (end <= start) {
      errors.value.endDateTime = 'End date must be after start date'
      valid = false
    }
  }
  return valid
}

async function submitUpdate() {
  if (!booking.value || !canEdit.value) return
  if (!validate()) return

  saving.value = true
  saveError.value = ''
  saveSuccess.value = false

  const dto: UpdateBookingDto = {
    id: booking.value.id,
    startDateTime: new Date(form.value.startDateTime).toISOString(),
    endDateTime: new Date(form.value.endDateTime).toISOString(),
    purpose: form.value.purpose || undefined,
  }

  try {
    booking.value = await bookingService.update(booking.value.id, dto)
    saveSuccess.value = true
    form.value.startDateTime = toLocalInput(booking.value.startDateTime)
    form.value.endDateTime = toLocalInput(booking.value.endDateTime)
    form.value.purpose = booking.value.purpose ?? ''
    setTimeout(() => {
      router.push({ name: 'booking-detail', params: { id: booking.value!.id } })
    }, 1200)
  } catch (err: unknown) {
    saveError.value = err instanceof Error ? err.message : 'Failed to update booking'
  } finally {
    saving.value = false
  }
}

function onCalendarDateSelect(dateStr: string) {
  const startVal = form.value.startDateTime
  const endVal = form.value.endDateTime

  if (!startVal || (startVal && endVal)) {
    form.value.startDateTime = `${dateStr}T09:00`
    form.value.endDateTime = ''
  } else {
    if (dateStr >= startVal.slice(0, 10)) {
      form.value.endDateTime = `${dateStr}T17:00`
    } else {
      form.value.endDateTime = `${form.value.startDateTime.slice(0, 10)}T17:00`
      form.value.startDateTime = `${dateStr}T09:00`
    }
  }
}

onMounted(loadBooking)
</script>
