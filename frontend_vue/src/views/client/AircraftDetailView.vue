<template>
  <div>
    <button
      class="bg-transparent border-none cursor-pointer text-blue-600 text-base font-medium p-0 mb-6 transition-colors hover:text-blue-700 hover:underline"
      @click="$router.back()"
    >← Back</button>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadAircraft" />

    <template v-else-if="aircraft">
      <div class="flex justify-between items-start mb-6 gap-4 flex-wrap">
        <div>
          <p class="text-sm font-bold text-slate-500 font-mono mb-1">{{ aircraft.registrationNumber }}</p>
          <h1 class="text-2xl font-bold text-slate-900 mb-1 tracking-tight">{{ aircraft.make }} {{ aircraft.model }}</h1>
          <p class="text-base text-slate-500">{{ aircraft.year }} · {{ aircraft.category }}</p>
        </div>
        <span :class="detailBadgeClass(aircraft)">
          {{ detailBadgeLabel(aircraft) }}
        </span>
      </div>

      <!-- Photo Gallery -->
      <section v-if="resolvedPhotos.length > 0 || heroPhotoUrl" class="mb-6 bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm">
        <div class="w-full h-80 bg-slate-100">
          <img
            v-if="heroPhotoUrl && !heroImageBroken"
            :src="heroPhotoUrl"
            :alt="`${aircraft.make} ${aircraft.model}`"
            class="w-full h-full object-cover block"
            @error="heroImageBroken = true"
          />
          <div v-else class="w-full h-full flex items-center justify-center text-7xl text-slate-300">✈</div>
        </div>
        <div v-if="resolvedPhotos.length > 1" class="flex gap-2 p-3 overflow-x-auto bg-slate-50">
          <button
            v-for="(photo, idx) in resolvedPhotos"
            :key="photo.id"
            :class="[
              'shrink-0 w-16 h-12 rounded overflow-hidden cursor-pointer p-0 bg-transparent border-2 transition-colors',
              idx === selectedPhotoIndex ? 'border-blue-600' : 'border-transparent hover:border-blue-500'
            ]"
            @click="selectedPhotoIndex = idx; heroImageBroken = false"
          >
            <img :src="photo.resolvedUrl" :alt="photo.description || 'Photo'" class="w-full h-full object-cover block" @error="onThumbError($event)" />
          </button>
        </div>
      </section>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <!-- Left column: Specs + Owner stacked -->
        <div class="flex flex-col gap-4">
          <section class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
            <h2 class="text-lg font-semibold text-slate-900 mb-4">Specifications</h2>
            <dl class="grid grid-cols-2 gap-3">
              <div class="flex flex-col gap-0.5" v-for="(val, key) in specs" :key="key">
                <dt class="text-xs text-slate-500 font-medium uppercase tracking-wider">{{ key }}</dt>
                <dd class="text-base text-slate-900 font-medium m-0">{{ val }}</dd>
              </div>
            </dl>
          </section>

          <!-- Owner info -->
          <section v-if="aircraft.companyName" class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
            <h2 class="text-lg font-semibold text-slate-900 mb-4">Owner</h2>
            <dl class="grid grid-cols-2 gap-3">
              <div class="flex flex-col gap-0.5">
                <dt class="text-xs text-slate-500 font-medium uppercase tracking-wider">Company</dt>
                <dd class="text-base text-slate-900 font-medium m-0">{{ aircraft.companyName }}</dd>
              </div>
              <div v-if="aircraft.companyEmail" class="flex flex-col gap-0.5">
                <dt class="text-xs text-slate-500 font-medium uppercase tracking-wider">Email</dt>
                <dd class="text-base text-slate-900 font-medium m-0">
                  <a :href="'mailto:' + aircraft.companyEmail" class="text-blue-600 no-underline transition-colors hover:text-blue-700 hover:underline">{{ aircraft.companyEmail }}</a>
                </dd>
              </div>
              <div v-if="aircraft.companyPhone" class="flex flex-col gap-0.5">
                <dt class="text-xs text-slate-500 font-medium uppercase tracking-wider">Phone</dt>
                <dd class="text-base text-slate-900 font-medium m-0">
                  <a :href="'tel:' + aircraft.companyPhone" class="text-blue-600 no-underline transition-colors hover:text-blue-700 hover:underline">{{ aircraft.companyPhone }}</a>
                </dd>
              </div>
            </dl>
          </section>
        </div>

        <!-- Booking section -->
        <section v-if="aircraft.isAvailable || aircraft.status === 'Maintenance' || aircraft.status === 'InsuranceInactive'" class="bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">
            {{ aircraft.status === 'InsuranceInactive' ? 'Availability' : 'Book this aircraft' }}
          </h2>

          <p v-if="aircraft.status === 'InsuranceInactive'" class="text-base font-semibold text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-3">
            ⚠ Insurance currently inactive — days without coverage are shown as unavailable
          </p>

          <AvailabilityCalendar
            :aircraft-id="aircraft.id"
            :selected-start="bookForm.startDateTime"
            :selected-end="bookForm.endDateTime"
            :insurance-policies="aircraft.insurancePolicies ?? []"
            @select-date="onCalendarDateSelect"
          />

          <template v-if="aircraft.status !== 'InsuranceInactive' || (aircraft.insurancePolicies && aircraft.insurancePolicies.length > 0)">
            <AppAlert v-if="bookingError" type="error" class="mb-4">{{ bookingError }}</AppAlert>
            <AppAlert v-if="bookingSuccess" type="success" class="mb-4">
              Booking requested! You will receive confirmation shortly.
            </AppAlert>

            <form @submit.prevent="submitBooking" novalidate class="flex flex-col gap-4">
              <AppInput
                v-model="bookForm.startDateTime"
                label="Start date & time"
                type="datetime-local"
                required
                :error="bookErrors.startDateTime"
              />
              <AppInput
                v-model="bookForm.endDateTime"
                label="End date & time"
                type="datetime-local"
                required
                :error="bookErrors.endDateTime"
              />
              <AppInput
                v-model="bookForm.purpose"
                label="Purpose / notes"
                placeholder="e.g. Training flight to Helsinki"
              />
              <AppButton type="submit" block :loading="booking">Request booking</AppButton>
            </form>
          </template>
        </section>
      </div>

      <!-- Reviews section -->
      <section class="mt-6 bg-white border border-slate-200 rounded-xl px-6 py-5 shadow-sm">
        <div class="flex items-center gap-4 mb-4">
          <h2 class="text-lg font-semibold text-slate-900 m-0">
            Reviews
            <span v-if="aircraft.reviewCount > 0" class="text-slate-500 font-normal">({{ aircraft.reviewCount }})</span>
          </h2>
          <div v-if="aircraft.averageRating > 0" class="flex items-center gap-1 text-base font-semibold text-slate-900">
            <span class="text-amber-400">★</span>
            <span>{{ aircraft.averageRating.toFixed(1) }}</span>
          </div>
        </div>

        <LoadingSpinner v-if="reviewsLoading" />

        <div v-else-if="reviews.length === 0" class="text-base text-slate-500">
          No reviews yet for this aircraft.
        </div>

        <div v-else class="flex flex-col gap-4">
          <div v-for="review in reviews" :key="review.id" class="p-4 bg-slate-50 rounded-lg">
            <div class="flex items-center gap-3 mb-2 flex-wrap">
              <span class="text-base font-semibold text-slate-800">{{ review.authorName }}</span>
              <div class="flex gap-px text-base">
                <span
                  v-for="star in 5"
                  :key="star"
                  :class="star <= review.rating ? 'text-amber-400' : 'text-slate-200'"
                >★</span>
              </div>
              <span class="text-xs text-slate-500 ml-auto">{{ formatDate(review.reviewedAt) }}</span>
            </div>
            <p v-if="review.comment" class="text-base text-slate-600 m-0 mb-2 leading-relaxed">{{ review.comment }}</p>
            <div class="flex gap-2 items-center">
              <span v-if="review.reviewType" class="bg-blue-50 text-blue-600 text-xs font-medium px-2 py-0.5 rounded-full">{{ review.reviewType }}</span>
              <span v-if="review.isVerifiedBooking" class="text-xs text-emerald-500 font-medium">✓ Verified</span>
            </div>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { aircraftService, aircraftPhotoService, bookingService, reviewService } from '@/api'
import { ApiError } from '@/types/api'
import type { AircraftDto, AircraftPhotoDto } from '@/types'
import type { ReviewDto } from '@/types/review'
import { resolvePhotoUrl, resolvePhotoUrls } from '@/utils/photoUrl'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import AvailabilityCalendar from '@/components/common/AvailabilityCalendar.vue'

const route = useRoute()
const aircraft = ref<AircraftDto | null>(null)
const loading = ref(false)
const error = ref('')
const booking = ref(false)
const bookingError = ref('')
const bookingSuccess = ref(false)

const bookForm = ref({ startDateTime: '', endDateTime: '', purpose: '' })
const bookErrors = ref({ startDateTime: '', endDateTime: '' })

function detailBadgeLabel(ac: AircraftDto): string {
  switch (ac.status) {
    case 'InsuranceInactive': return 'Insurance Inactive'
    case 'Maintenance': return 'Maintenance'
    case 'Unavailable': return 'Unavailable'
    case 'Available': return 'Available'
    default: return ac.isAvailable ? 'Available' : 'Unavailable'
  }
}

const badgeBase = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold'

function detailBadgeClass(ac: AircraftDto): string {
  const map: Record<string, string> = {
    InsuranceInactive: `${badgeBase} bg-amber-100 text-amber-700`,
    Maintenance: `${badgeBase} bg-blue-100 text-blue-700`,
    Unavailable: `${badgeBase} bg-red-100 text-red-700`,
    Available: `${badgeBase} bg-emerald-100 text-emerald-700`,
  }
  if (ac.status && map[ac.status]) return map[ac.status]
  return ac.isAvailable
    ? `${badgeBase} bg-emerald-100 text-emerald-700`
    : `${badgeBase} bg-red-100 text-red-700`
}

// Photos
const photos = ref<AircraftPhotoDto[]>([])
const photosLoading = ref(false)
const selectedPhotoIndex = ref(0)
const heroImageBroken = ref(false)

/** Hide broken thumbnail images */
function onThumbError(e: Event) {
  const img = e.target as HTMLImageElement
  img.style.display = 'none'
}

const resolvedPhotos = computed(() =>
  photos.value.map(p => ({ ...p, resolvedUrl: resolvePhotoUrl(p.url) }))
)

const heroPhotoUrl = computed(() => {
  if (resolvedPhotos.value.length > 0) {
    return resolvedPhotos.value[selectedPhotoIndex.value]?.resolvedUrl || ''
  }
  // Fallback to photoUrls from aircraft DTO
  const urls = resolvePhotoUrls(aircraft.value?.photoUrls)
  return urls.length > 0 ? urls[0] : ''
})

// Reviews
const reviews = ref<ReviewDto[]>([])
const reviewsLoading = ref(false)

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

const specs = computed(() => aircraft.value ? {
  'Registration': aircraft.value.registrationNumber,
  'Make': aircraft.value.make,
  'Model': aircraft.value.model,
  'Year': aircraft.value.year,
  'Category': aircraft.value.category,
  'Required License': aircraft.value.requiredLicenseType || '—',
  'Rate': aircraft.value.hourlyRate ? `€${aircraft.value.hourlyRate.toFixed(2)}/hr` : 'N/A',
} : {})

async function loadPhotos(aircraftId: string) {
  photosLoading.value = true
  try {
    photos.value = await aircraftPhotoService.getAll(aircraftId)
    selectedPhotoIndex.value = 0
  } catch {
    photos.value = []
  } finally {
    photosLoading.value = false
  }
}

async function loadAircraft() {
  loading.value = true
  error.value = ''
  try {
    aircraft.value = await aircraftService.getById(route.params.id as string)
    const id = route.params.id as string
    loadPhotos(id)
    loadReviews(id)
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load aircraft'
  } finally {
    loading.value = false
  }
}

async function loadReviews(aircraftId: string) {
  reviewsLoading.value = true
  try {
    reviews.value = await reviewService.getByAircraft(aircraftId)
  } catch {
    // Non-critical
  } finally {
    reviewsLoading.value = false
  }
}

async function submitBooking() {
  bookErrors.value = { startDateTime: '', endDateTime: '' }
  if (!bookForm.value.startDateTime) { bookErrors.value.startDateTime = 'Required'; return }
  if (!bookForm.value.endDateTime) { bookErrors.value.endDateTime = 'Required'; return }
  if (new Date(bookForm.value.endDateTime) <= new Date(bookForm.value.startDateTime)) {
    bookErrors.value.endDateTime = 'End must be after start'; return
  }

  booking.value = true
  bookingError.value = ''
  try {
    await bookingService.create({
      aircraftId: aircraft.value!.id,
      startDateTime: bookForm.value.startDateTime,
      endDateTime: bookForm.value.endDateTime,
      purpose: bookForm.value.purpose || undefined,
    })
    bookingSuccess.value = true
    bookForm.value = { startDateTime: '', endDateTime: '', purpose: '' }
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      bookingError.value = err.apiMessage || err.message || 'Booking failed'
    } else if (err instanceof Error) {
      bookingError.value = err.message || 'Booking failed'
    } else {
      bookingError.value = 'Booking failed. Please try again.'
    }
  } finally {
    booking.value = false
  }
}

function onCalendarDateSelect(dateStr: string) {
  const startVal = bookForm.value.startDateTime
  const endVal = bookForm.value.endDateTime

  if (!startVal || (startVal && endVal)) {
    bookForm.value.startDateTime = `${dateStr}T09:00`
    bookForm.value.endDateTime = ''
  } else {
    if (dateStr >= startVal.slice(0, 10)) {
      bookForm.value.endDateTime = `${dateStr}T17:00`
    } else {
      bookForm.value.endDateTime = `${bookForm.value.startDateTime.slice(0, 10)}T17:00`
      bookForm.value.startDateTime = `${dateStr}T09:00`
    }
  }
}

onMounted(loadAircraft)
</script>
