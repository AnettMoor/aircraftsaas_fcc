<template>
  <div>
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-slate-900 m-0 tracking-tight">Browse Aircraft</h1>
    </div>

    <!-- Filters -->
    <div class="mb-6 bg-slate-50 border border-slate-200 rounded-xl p-5">
      <div class="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4 mb-4">
        <AppInput
          v-model="filters.search"
          placeholder="Search by make or model…"
          label="Search"
          @input="debouncedSearch"
        />

        <AppSelect
          v-model="filters.category"
          label="Category"
          :options="categoryOptions"
          placeholder="All Categories"
          @update:model-value="loadAircraft"
        />

        <AppSelect
          v-model="filters.location"
          label="Base Airport"
          :options="airportOptions"
          placeholder="All Airports"
          @update:model-value="loadAircraft"
        />

        <AppSelect
          v-model="filters.status"
          label="Status"
          :options="statusOptions"
          placeholder="All Statuses"
          @update:model-value="loadAircraft"
        />
      </div>

      <div class="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4 mb-3">
        <AppInput
          v-model="filters.maxHourlyRate"
          type="number"
          label="Max Hourly Rate (€)"
          placeholder="e.g. 500"
          @input="debouncedSearch"
        />

        <AppInput
          v-model="filters.year"
          type="number"
          label="Year"
          placeholder="e.g. 2020"
          @input="debouncedSearch"
        />

        <AppInput
          v-model="filters.startDate"
          type="date"
          label="Available From"
          @input="debouncedSearch"
        />

        <AppInput
          v-model="filters.endDate"
          type="date"
          label="Available Until"
          @input="debouncedSearch"
        />
      </div>

      <div class="flex items-center gap-4 pt-2 border-t border-slate-200">
        <AppButton variant="secondary" size="sm" @click="clearFilters">
          Clear Filters
        </AppButton>
        <span v-if="activeFilterCount > 0" class="text-xs text-slate-600 font-medium">
          {{ activeFilterCount }} filter{{ activeFilterCount > 1 ? 's' : '' }} active
        </span>
      </div>
    </div>

    <LoadingSpinner v-if="loading" />

    <ErrorState
      v-else-if="error"
      :message="error"
      retryable
      @retry="loadAircraft"
    />

    <EmptyState
      v-else-if="items.length === 0"
      icon="✈"
      title="No aircraft found"
      description="Try adjusting your filters or check back later."
    />

    <div v-else class="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-4">
      <div
        v-for="aircraft in items"
        :key="aircraft.id"
        class="bg-white border border-slate-200 rounded-xl overflow-hidden cursor-pointer shadow-sm transition-all duration-150 hover:shadow-md hover:border-slate-300 hover:-translate-y-0.5"
        @click="$router.push({ name: 'aircraft-detail', params: { id: aircraft.id } })"
      >
        <div class="w-full h-[180px] bg-slate-50">
          <img
            v-if="primaryPhoto(aircraft) && !brokenImages.has(aircraft.id)"
            :src="primaryPhoto(aircraft)"
            :alt="`${aircraft.make} ${aircraft.model}`"
            class="w-full h-full object-cover block"
            @error="onImgError(aircraft.id)"
          />
          <div v-else class="w-full h-full flex items-center justify-center text-4xl text-slate-300 bg-slate-100">✈</div>
        </div>
        <div class="p-5">
          <div class="flex justify-between items-center mb-2">
            <span class="text-xs font-bold text-slate-600 font-mono">{{ aircraft.registrationNumber }}</span>
            <span :class="statusBadgeClasses(aircraft)">
              {{ clientStatusLabel(aircraft) }}
            </span>
          </div>
          <h3 class="text-base font-semibold text-slate-900 m-0 mb-1">{{ aircraft.make }} {{ aircraft.model }}</h3>
          <p class="text-xs text-slate-500 m-0 mb-1">
            {{ aircraft.year }} · {{ aircraft.category }}
          </p>
          <p class="text-xs text-slate-600 m-0 mb-2" v-if="aircraft.baseAirportName">
            📍 {{ aircraft.baseAirportName }}
          </p>
          <p v-if="aircraft.hourlyRate" class="text-sm font-semibold text-blue-600 m-0">
            €{{ aircraft.hourlyRate.toFixed(2) }} / hour
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { aircraftService, airportService } from '@/api'
import type { AircraftDto, AircraftSearchParams, AirportDto } from '@/types'
import { resolvePhotoUrl } from '@/utils/photoUrl'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppSelect from '@/components/common/AppSelect.vue'
import AppButton from '@/components/common/AppButton.vue'
import type { SelectOption } from '@/components/common/AppSelect.vue'

function primaryPhoto(aircraft: AircraftDto): string {
  if (aircraft.photoUrls && aircraft.photoUrls.length > 0) {
    return resolvePhotoUrl(aircraft.photoUrls[0])
  }
  return ''
}

function clientStatusLabel(ac: AircraftDto): string {
  switch (ac.status) {
    case 'InsuranceInactive': return 'Insurance Inactive'
    case 'Maintenance': return 'Maintenance'
    case 'Unavailable': return 'Unavailable'
    case 'Available': return 'Available'
    default: return ac.isAvailable ? 'Available' : 'Unavailable'
  }
}

const statusColorMap: Record<string, string> = {
  ok: 'bg-emerald-100 text-emerald-800',
  busy: 'bg-red-100 text-red-800',
  insurance: 'bg-amber-100 text-amber-800',
  maintenance: 'bg-blue-100 text-blue-800',
}

function statusBadgeClasses(ac: AircraftDto): string {
  const base = 'text-xs font-medium px-2 py-0.5 rounded-full'
  let variant = 'ok'
  switch (ac.status) {
    case 'InsuranceInactive': variant = 'insurance'; break
    case 'Maintenance': variant = 'maintenance'; break
    case 'Unavailable': variant = 'busy'; break
    case 'Available': variant = 'ok'; break
    default: variant = ac.isAvailable ? 'ok' : 'busy'
  }
  return `${base} ${statusColorMap[variant]}`
}

const items = ref<AircraftDto[]>([])
const loading = ref(false)
const error = ref('')
// cleaner api, react with Set methods directly without .value
const brokenImages = reactive(new Set<string>())

function onImgError(aircraftId: string) {
  brokenImages.add(aircraftId)
}
const airports = ref<AirportDto[]>([])
let debounceTimer: ReturnType<typeof setTimeout> //prevents loadaircraft from being called on every keystroke

  // flat object with multiple properties instead of multiple refs
const filters = reactive({
  search: '',
  category: '',
  location: '',
  status: '',
  maxHourlyRate: '',
  year: '',
  startDate: '',
  endDate: '',
})

const categoryOptions: SelectOption[] = [
  { value: 'SingleEngine', label: 'Single Engine' },
  { value: 'MultiEngine', label: 'Multi Engine' },
  { value: 'Helicopter', label: 'Helicopter' },
  { value: 'Jet', label: 'Jet' },
  { value: 'Turboprop', label: 'Turboprop' },
]

const statusOptions: SelectOption[] = [
  { value: 'InsuranceInactive', label: 'Insurance Inactive' },
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Available', label: 'Available' },
]

const airportOptions = computed<SelectOption[]>(() =>
  airports.value.map(a => ({
    value: a.name,
    label: `${a.name} (${a.icaoCode})`,
  }))
)

const activeFilterCount = computed(() => {
  let count = 0
  if (filters.search) count++
  if (filters.category) count++
  if (filters.location) count++
  if (filters.status) count++
  if (filters.maxHourlyRate) count++
  if (filters.year) count++
  if (filters.startDate) count++
  if (filters.endDate) count++
  return count
})

function buildSearchParams(): AircraftSearchParams | undefined {
  const params: AircraftSearchParams = {}
  let hasParams = false

  if (filters.search) { params.make = filters.search; hasParams = true }
  if (filters.category) { params.category = filters.category; hasParams = true }
  if (filters.location) { params.location = filters.location; hasParams = true }
  if (filters.status) { params.status = filters.status; hasParams = true }
  if (filters.maxHourlyRate) {
    const rate = parseFloat(filters.maxHourlyRate)
    if (!isNaN(rate) && rate > 0) { params.maxHourlyRate = rate; hasParams = true }
  }
  if (filters.year) {
    const yr = parseInt(filters.year, 10)
    if (!isNaN(yr) && yr > 1900) { params.year = yr; hasParams = true }
  }
  if (filters.startDate) { params.startDate = filters.startDate; hasParams = true }
  if (filters.endDate) { params.endDate = filters.endDate; hasParams = true }

  return hasParams ? params : undefined
}

function debouncedSearch() {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(loadAircraft, 350)
}

async function loadAircraft() {
  loading.value = true
  error.value = ''
  try {
    const all = await aircraftService.search(buildSearchParams())
    items.value = all
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load aircraft'
  } finally {
    loading.value = false
  }
}

function clearFilters() {
  filters.search = ''
  filters.category = ''
  filters.location = ''
  filters.status = ''
  filters.maxHourlyRate = ''
  filters.year = ''
  filters.startDate = ''
  filters.endDate = ''
  loadAircraft()
}

async function loadAirports() {
  try {
    airports.value = await airportService.getAll()
  } catch {
    // Non-critical
  }
}

onMounted(() => {
  loadAircraft()
  loadAirports()
})
</script>
