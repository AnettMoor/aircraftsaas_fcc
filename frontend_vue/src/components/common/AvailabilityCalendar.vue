<template>
  <div class="select-none">
    <div class="flex items-center justify-between mb-3">
      <button
        class="w-8 h-8 bg-white border border-slate-200 rounded flex items-center justify-center text-xl text-slate-700 cursor-pointer hover:bg-slate-50 hover:border-slate-300 transition-all"
        @click="prevMonth"
        aria-label="Previous month"
      >‹</button>
      <span class="text-base font-semibold text-slate-900">{{ monthLabel }}</span>
      <button
        class="w-8 h-8 bg-white border border-slate-200 rounded flex items-center justify-center text-xl text-slate-700 cursor-pointer hover:bg-slate-50 hover:border-slate-300 transition-all"
        @click="nextMonth"
        aria-label="Next month"
      >›</button>
    </div>

    <div class="grid grid-cols-7 gap-[3px]">
      <div v-for="day in weekDays" :key="day" class="text-center text-xs font-semibold text-slate-500 py-1 uppercase tracking-wide">{{ day }}</div>

      <div
        v-for="(cell, idx) in calendarCells"
        :key="idx"
        :class="cellClasses(cell)"
        :title="cellTitle(cell)"
        @click="cell.day && cell.status === 'available' ? onDayClick(cell) : undefined"
      >
        <span v-if="cell.day">{{ cell.day }}</span>
      </div>
    </div>

    <div class="flex gap-4 mt-3 flex-wrap py-3 border-t border-slate-100">
      <span class="flex items-center gap-1.5 text-xs text-slate-600 font-medium">
        <span class="w-2.5 h-2.5 rounded-full shrink-0 bg-emerald-100 border border-emerald-800"></span> Available
      </span>
      <span class="flex items-center gap-1.5 text-xs text-slate-600 font-medium">
        <span class="w-2.5 h-2.5 rounded-full shrink-0 bg-amber-100 border border-amber-800"></span> Booked
      </span>
      <span class="flex items-center gap-1.5 text-xs text-slate-600 font-medium">
        <span class="w-2.5 h-2.5 rounded-full shrink-0 bg-pink-100 border border-pink-800"></span> Maintenance
      </span>
      <span class="flex items-center gap-1.5 text-xs text-slate-600 font-medium">
        <span class="w-2.5 h-2.5 rounded-full shrink-0 bg-red-100 border border-red-800"></span> Unavailable
      </span>
      <span class="flex items-center gap-1.5 text-xs text-slate-600 font-medium">
        <span class="w-2.5 h-2.5 rounded-full shrink-0 bg-blue-600 border border-blue-700"></span> Selected
      </span>
    </div>

    <LoadingSpinner v-if="loadingAvailability" />
    <p v-if="availError" class="text-sm text-red-500 mt-2">{{ availError }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { availabilityService } from '@/api'
import type { AircraftAvailabilityDto } from '@/types/availability'
import type { InsurancePolicyDto } from '@/types/insurance'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'

// ── Props & Emits ──────────────────────────────────────────────
interface Props {
  aircraftId: string
  /** ISO date string YYYY-MM-DD or datetime-local value */
  selectedStart?: string
  /** ISO date string YYYY-MM-DD or datetime-local value */
  selectedEnd?: string
  /**
   * When editing an existing booking, pass the original start/end ISO datetime
   * strings so the calendar excludes that booking's "Booked" slots and treats
   * those dates as available.
   */
  excludeBookingStart?: string
  excludeBookingEnd?: string
  /**
   * Insurance policies for this aircraft.
   * When provided, each day is checked against policy date ranges.
   * Days not covered by any policy are marked 'unavailable' (no insurance).
   * When not provided (undefined/empty), insurance is assumed OK (no restriction).
   *
   * @deprecated forceAllUnavailable — replaced by per-day insurance checking
   */
  insurancePolicies?: InsurancePolicyDto[]
  /** @deprecated Use insurancePolicies instead. Kept for backward compat. */
  forceAllUnavailable?: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'select-date': [dateStr: string]
}>()

// ── State ──────────────────────────────────────────────────────
const currentYear = ref(new Date().getFullYear())
const currentMonth = ref(new Date().getMonth()) // 0-based
const availability = ref<AircraftAvailabilityDto[]>([])
const loadingAvailability = ref(false)
const availError = ref('')

const weekDays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

// ── Computed ───────────────────────────────────────────────────
const monthLabel = computed(() => {
  const d = new Date(currentYear.value, currentMonth.value, 1)
  return d.toLocaleString('en-GB', { month: 'long', year: 'numeric' })
})

type CellStatus = 'available' | 'unavailable' | 'booked' | 'maintenance' | 'none'

interface CalendarCell {
  day: number | null
  date: Date | null
  status: CellStatus
}

const calendarCells = computed<CalendarCell[]>(() => {
  const year = currentYear.value
  const month = currentMonth.value
  const firstDay = new Date(year, month, 1)
  const lastDay = new Date(year, month + 1, 0)
  const daysInMonth = lastDay.getDate()

  // Monday = 0, Sunday = 6  (JS getDay(): Sun=0 .. Sat=6)
  let startWeekday = firstDay.getDay() - 1
  if (startWeekday < 0) startWeekday = 6

  const cells: CalendarCell[] = []

  // Empty leading cells
  for (let i = 0; i < startWeekday; i++) {
    cells.push({ day: null, date: null, status: 'none' })
  }

  // Day cells
  for (let d = 1; d <= daysInMonth; d++) {
    const date = new Date(year, month, d)
    cells.push({ day: d, date, status: getDayStatus(date) })
  }

  return cells
})

// ── Helpers ────────────────────────────────────────────────────

/**
 * Check whether the given date is covered by at least one insurance policy.
 * Returns true if no insurance policies are provided (assume OK).
 */
function isDayCoveredByInsurance(date: Date): boolean {
  // If no policies provided, insurance is assumed OK (no restriction)
  if (!props.insurancePolicies || props.insurancePolicies.length === 0) {
    // Backward compat: if forceAllUnavailable is true and no policies → not covered
    return !props.forceAllUnavailable
  }

  const dayStart = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0)
  const dayEnd = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59)

  return props.insurancePolicies.some(policy => {
    const policyStart = new Date(policy.startDate)
    const policyEnd = new Date(policy.endDate)
    // Day is covered if the policy range overlaps with the day
    return policyStart <= dayEnd && policyEnd >= dayStart
  })
}

/**
 * Check whether a "Booked" slot belongs to the booking currently being edited.
 * If excludeBookingStart/End are provided, any Booked slot whose range overlaps
 * with the excluded range is considered "our" booking and should be ignored.
 */
function isExcludedBookingSlot(slot: AircraftAvailabilityDto): boolean {
  if (!props.excludeBookingStart || !props.excludeBookingEnd) return false
  if (slot.availabilityType !== 'Booked') return false

  const exStart = new Date(props.excludeBookingStart).getTime()
  const exEnd = new Date(props.excludeBookingEnd).getTime()
  const slotStart = new Date(slot.startDateTime).getTime()
  const slotEnd = new Date(slot.endDateTime).getTime()

  // Slot matches if its range overlaps with the excluded booking range
  return slotStart < exEnd && slotEnd > exStart
}

/** Determine whether a given date is available, unavailable, booked, maintenance, or unknown */
function getDayStatus(date: Date): CellStatus {
  // Check per-day insurance coverage
  if (!isDayCoveredByInsurance(date)) return 'unavailable'

  const dayStart = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0)
  const dayEnd = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59)

  let isAvailable = false
  let isUnavailable = false
  let isBooked = false
  let isMaintenance = false

  for (const slot of availability.value) {
    // Skip "Booked" slots belonging to the booking we are editing
    if (isExcludedBookingSlot(slot)) continue

    const slotStart = new Date(slot.startDateTime)
    const slotEnd = new Date(slot.endDateTime)

    // Check if this day overlaps with the availability slot
    if (slotStart <= dayEnd && slotEnd >= dayStart) {
      if (slot.availabilityType === 'Available') {
        isAvailable = true
      } else if (slot.availabilityType === 'Booked') {
        isBooked = true
      } else if (slot.availabilityType === 'Maintenance') {
        isMaintenance = true
      } else {
        // Blocked
        isUnavailable = true
      }
    }
  }

  // Maintenance and blocked take precedence, then booked, then available
  if (isMaintenance) return 'maintenance'
  if (isUnavailable) return 'unavailable'
  if (isBooked) return 'booked'

  if (isAvailable) return 'available'

  return 'none'
}

function toDateStr(d: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function isSelected(cell: CalendarCell): boolean {
  if (!cell.date) return false
  const cellStr = toDateStr(cell.date)
  const startStr = props.selectedStart ? props.selectedStart.slice(0, 10) : ''
  const endStr = props.selectedEnd ? props.selectedEnd.slice(0, 10) : ''

  if (startStr && endStr && startStr !== endStr) {
    return cellStr >= startStr && cellStr <= endStr
  }
  return cellStr === startStr || cellStr === endStr
}

const cellBase = 'aspect-square flex items-center justify-center rounded text-sm font-medium cursor-default transition-all relative'

const statusClasses: Record<CellStatus | 'empty', string> = {
  none: 'bg-slate-50 text-slate-400 cursor-not-allowed',
  available: 'bg-emerald-100 text-emerald-800 cursor-pointer hover:bg-emerald-200 hover:scale-[1.08] hover:shadow-sm',
  booked: 'bg-amber-100 text-amber-800 cursor-not-allowed',
  unavailable: 'bg-red-100 text-red-800 cursor-not-allowed font-semibold',
  maintenance: 'bg-pink-100 text-pink-800 cursor-not-allowed',
  empty: 'bg-transparent',
}

function cellClasses(cell: CalendarCell): string {
  const parts = [cellBase]

  if (!cell.day) {
    parts.push(statusClasses.empty)
  } else {
    parts.push(statusClasses[cell.status] || statusClasses.none)
  }

  if (cell.date && cell.date < new Date(new Date().setHours(0, 0, 0, 0))) {
    parts.push('opacity-40 pointer-events-none')
  }

  if (isSelected(cell)) {
    parts.push('!bg-blue-600 !text-white font-bold ring-2 ring-blue-200')
  }

  return parts.join(' ')
}

function cellTitle(cell: CalendarCell): string {
  if (!cell.day) return ''
  if (cell.status === 'available') return 'Available for booking'
  if (cell.status === 'booked') return 'Already booked'
  if (cell.status === 'maintenance') return 'Under maintenance — not available'
  if (cell.status === 'unavailable') {
    // If insurance policies are provided, check if this specific day lacks insurance
    if (cell.date && props.insurancePolicies && props.insurancePolicies.length > 0) {
      if (!isDayCoveredByInsurance(cell.date)) {
        return 'No active insurance — not available'
      }
    }
    return 'Not available'
  }
  return 'No availability set'
}

function onDayClick(cell: CalendarCell) {
  if (!cell.date || cell.status !== 'available') return
  // Emit date as YYYY-MM-DD so parent can use it
  emit('select-date', toDateStr(cell.date))
}

// ── Navigation ─────────────────────────────────────────────────
function prevMonth() {
  if (currentMonth.value === 0) {
    currentMonth.value = 11
    currentYear.value--
  } else {
    currentMonth.value--
  }
}

function nextMonth() {
  if (currentMonth.value === 11) {
    currentMonth.value = 0
    currentYear.value++
  } else {
    currentMonth.value++
  }
}

// ── Data fetching ──────────────────────────────────────────────
async function loadAvailability() {
  loadingAvailability.value = true
  availError.value = ''
  try {
    availability.value = await availabilityService.getAll(props.aircraftId)
  } catch {
    availError.value = 'Failed to load availability data'
  } finally {
    loadingAvailability.value = false
  }
}

watch(() => props.aircraftId, () => {
  if (props.aircraftId) loadAvailability()
})

onMounted(() => {
  if (props.aircraftId) loadAvailability()
})
</script>
