<template>
  <div>
    <div class="mb-6 flex flex-col gap-1">
      <RouterLink :to="{ name: 'admin-aircraft' }" class="text-sm text-blue-600 font-medium hover:text-blue-700 hover:underline transition-colors">← Back to aircraft</RouterLink>
      <h1 class="text-2xl font-bold tracking-tight text-slate-900">{{ aircraft?.registrationNumber || 'Aircraft Detail' }}</h1>
      <p v-if="aircraft" class="text-base text-slate-500 m-0">{{ aircraft.make }} {{ aircraft.model }} ({{ aircraft.year }})</p>
    </div>

    <LoadingSpinner v-if="loadingAircraft" />
    <ErrorState v-else-if="aircraftError" :message="aircraftError" retryable @retry="loadAircraft" />

    <template v-else-if="aircraft">
      <!-- Tabs -->
      <div class="flex gap-1 border-b border-slate-200 mb-6 overflow-x-auto">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          :class="[
            'px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
            activeTab === tab.key
              ? 'border-blue-600 text-blue-600'
              : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
          ]"
          @click="activeTab = tab.key"
        >
          <span class="mr-1.5">{{ tab.icon }}</span>
          {{ tab.label }}
        </button>
      </div>

      <!-- General Tab -->
      <div v-if="activeTab === 'general'" class="min-h-[200px]">
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 bg-white border border-slate-200 rounded-xl shadow-sm p-6">
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Registration</span>
            <span class="text-sm text-slate-900 font-mono">{{ aircraft.registrationNumber }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Make / Model</span>
            <span class="text-sm text-slate-900">{{ aircraft.make }} {{ aircraft.model }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Year</span>
            <span class="text-sm text-slate-900">{{ aircraft.year }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Category</span>
            <span class="text-sm text-slate-900">{{ aircraft.category }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Required License</span>
            <span class="text-sm text-slate-900">{{ aircraft.requiredLicenseType || '—' }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Hourly Rate</span>
            <span class="text-sm text-slate-900">€{{ aircraft.hourlyRate.toFixed(2) }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Airspeed Hours</span>
            <span class="text-sm text-slate-900">{{ aircraft.totalAirspeedHours }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Base Airport</span>
            <span class="text-sm text-slate-900">{{ aircraft.baseAirportName || '—' }}</span>
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Status</span>
            <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold w-fit', detailStatusBadgeClass(aircraft)]">
              {{ detailStatusLabel(aircraft) }}
            </span>
          </div>
          <div class="flex flex-col gap-1 md:col-span-2 lg:col-span-3">
            <span class="text-xs font-medium text-slate-500 uppercase tracking-wider">Description</span>
            <span class="text-sm text-slate-900">{{ aircraft.description || '—' }}</span>
          </div>
        </div>
      </div>

      <!-- Photos Tab -->
      <div v-if="activeTab === 'photos'" class="min-h-[200px]">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-semibold text-slate-900 m-0">Photos</h2>
          <label class="inline-flex items-center gap-2 px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm text-slate-600 cursor-pointer hover:bg-slate-50 hover:border-slate-300 transition-colors">
            📷 Upload photo
            <input type="file" accept="image/*" class="hidden" @change="handlePhotoUpload" />
          </label>
        </div>
        <LoadingSpinner v-if="loadingPhotos" />
        <div v-else-if="photos.length === 0" class="text-slate-500 text-sm p-8 text-center bg-white border border-dashed border-slate-200 rounded-lg">No photos uploaded yet.</div>
        <div v-else class="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4">
          <div v-for="photo in photos" :key="photo.id" class="bg-white border border-slate-200 rounded-lg overflow-hidden shadow-sm hover:shadow-md transition-shadow">
            <img
              :src="resolvePhotoUrl(photo.url)"
              :alt="photo.description || 'Aircraft photo'"
              class="w-full h-40 object-cover block"
              @error="($event.target as HTMLImageElement).style.opacity = '0'"
            />
            <div class="flex gap-2 p-3">
              <button
                :class="[
                  'text-sm font-medium transition-colors',
                  photo.isPrimary
                    ? 'text-amber-600 cursor-default'
                    : 'text-blue-600 hover:text-blue-700 cursor-pointer'
                ]"
                @click="setPhotoPrimary(photo)"
                :disabled="photo.isPrimary"
              >
                {{ photo.isPrimary ? '★ Primary' : '☆ Set primary' }}
              </button>
              <button class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="deletePhoto(photo)">Delete</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Availability Tab -->
      <div v-if="activeTab === 'availability'" class="min-h-[200px]">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-semibold text-slate-900 m-0">Availability Blocks</h2>
          <AppButton @click="openAvailCreate">+ Add block</AppButton>
        </div>
        <LoadingSpinner v-if="loadingAvail" />
        <div v-else-if="availItems.length === 0" class="text-slate-500 text-sm p-8 text-center bg-white border border-dashed border-slate-200 rounded-lg">No availability blocks configured.</div>
        <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <table class="w-full text-left text-sm">
            <thead>
              <tr class="border-b border-slate-200 bg-slate-50">
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Type</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Start</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">End</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Reason</th>
                <th class="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="av in availItems" :key="av.id" class="border-b border-slate-100 hover:bg-slate-50 transition-colors">
                <td class="px-4 py-3">
                  <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold', availBadgeClass(av.availabilityType)]">
                    {{ av.availabilityType }}
                  </span>
                </td>
                <td class="px-4 py-3 text-slate-700">{{ formatDateTime(av.startDateTime) }}</td>
                <td class="px-4 py-3 text-slate-700">{{ formatDateTime(av.endDateTime) }}</td>
                <td class="px-4 py-3 text-slate-700">{{ av.reason || '—' }}</td>
                <td class="px-4 py-3">
                  <div v-if="av.availabilityType !== 'NoInsurance' && av.availabilityType !== 'Booked'" class="flex gap-2">
                    <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="openAvailEdit(av)">Edit</button>
                    <button class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="deleteAvail(av)">Delete</button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Availability form -->
        <div v-if="availFormVisible" class="mt-6 bg-white border border-slate-200 rounded-xl shadow-sm p-6">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">{{ availEditTarget ? 'Edit block' : 'Add availability block' }}</h2>
          <AppAlert v-if="availFormError" type="error" class="mb-4">{{ availFormError }}</AppAlert>
          <form @submit.prevent="saveAvail" novalidate class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div class="flex flex-col gap-1">
                <label class="text-sm font-medium text-slate-700">Type *</label>
                <select v-model="availForm.availabilityType" class="px-3 py-2 border border-slate-200 rounded-lg text-sm text-slate-700 bg-white min-h-[2.5rem] transition-colors focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15">
                  <option value="Available">Available</option>
                  <option value="Blocked">Blocked</option>
                  <option value="Maintenance">Maintenance</option>
                </select>
              </div>
              <AppInput v-model="availForm.reason" label="Reason" />
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <AppInput v-model="availForm.startDateTime" label="Start Date/Time" type="datetime-local" required />
              <AppInput v-model="availForm.endDateTime" label="End Date/Time" type="datetime-local" required />
            </div>
            <div class="flex gap-3 justify-end pt-4">
              <AppButton variant="secondary" type="button" @click="availFormVisible = false">Cancel</AppButton>
              <AppButton type="submit" :loading="savingAvail">{{ availEditTarget ? 'Save' : 'Create' }}</AppButton>
            </div>
          </form>
        </div>
      </div>

      <!-- Insurance Tab -->
      <div v-if="activeTab === 'insurance'" class="min-h-[200px]">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-semibold text-slate-900 m-0">Insurance Policies</h2>
          <AppButton @click="openInsCreate">+ Add policy</AppButton>
        </div>
        <LoadingSpinner v-if="loadingIns" />
        <div v-else-if="insItems.length === 0" class="text-slate-500 text-sm p-8 text-center bg-white border border-dashed border-slate-200 rounded-lg">No insurance policies found.</div>
        <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <table class="w-full text-left text-sm">
            <thead>
              <tr class="border-b border-slate-200 bg-slate-50">
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Policy #</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Provider</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Coverage</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Type</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Start</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">End</th>
                <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Status</th>
                <th class="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="ins in insItems" :key="ins.id" class="border-b border-slate-100 hover:bg-slate-50 transition-colors">
                <td class="px-4 py-3 font-mono text-slate-700">{{ ins.policyNumber }}</td>
                <td class="px-4 py-3 text-slate-700">{{ ins.insuranceProvider }}</td>
                <td class="px-4 py-3 text-slate-700">€{{ ins.coverageAmount.toLocaleString() }}</td>
                <td class="px-4 py-3 text-slate-700">{{ ins.coverageType }}</td>
                <td class="px-4 py-3 text-slate-700">{{ formatDate(ins.startDate) }}</td>
                <td class="px-4 py-3 text-slate-700">{{ formatDate(ins.endDate) }}</td>
                <td class="px-4 py-3">
                  <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold', ins.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800']">
                    {{ ins.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="px-4 py-3">
                  <div class="flex gap-2">
                    <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="openInsEdit(ins)">Edit</button>
                    <button class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="deleteIns(ins)">Delete</button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Insurance form -->
        <div v-if="insFormVisible" class="mt-6 bg-white border border-slate-200 rounded-xl shadow-sm p-6">
          <h2 class="text-lg font-semibold text-slate-900 mb-4">{{ insEditTarget ? 'Edit policy' : 'Add insurance policy' }}</h2>
          <AppAlert v-if="insFormError" type="error" class="mb-4">{{ insFormError }}</AppAlert>
          <form @submit.prevent="saveIns" novalidate class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <AppInput v-model="insForm.policyNumber" label="Policy Number" required />
              <AppInput v-model="insForm.insuranceProvider" label="Insurance Provider" required />
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <AppInput v-model="insForm.coverageAmount" label="Coverage Amount (€)" type="number" required />
              <AppInput v-model="insForm.coverageType" label="Coverage Type" required />
            </div>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <AppInput v-model="insForm.startDate" label="Start Date" type="date" required />
              <AppInput v-model="insForm.endDate" label="End Date" type="date" required />
            </div>
            <div class="flex gap-3 justify-end pt-4">
              <AppButton variant="secondary" type="button" @click="insFormVisible = false">Cancel</AppButton>
              <AppButton type="submit" :loading="savingIns">{{ insEditTarget ? 'Save' : 'Create' }}</AppButton>
            </div>
          </form>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { aircraftService, aircraftPhotoService, availabilityService, insuranceService } from '@/api'
import type { AircraftDto, AircraftPhotoDto, AircraftAvailabilityDto, InsurancePolicyDto } from '@/types'
import { resolvePhotoUrl } from '@/utils/photoUrl'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const route = useRoute()
const aircraftId = route.params.id as string

// ── General ──
const aircraft = ref<AircraftDto | null>(null)
const loadingAircraft = ref(false)
const aircraftError = ref('')

// ── Tabs ──
const activeTab = ref<'general' | 'photos' | 'availability' | 'insurance'>('general')
const tabs = [
  { key: 'general' as const, label: 'General', icon: '📋' },
  { key: 'photos' as const, label: 'Photos', icon: '📷' },
  { key: 'availability' as const, label: 'Availability', icon: '📅' },
  { key: 'insurance' as const, label: 'Insurance', icon: '🛡' },
]

// ── Photos ──
const photos = ref<AircraftPhotoDto[]>([])
const loadingPhotos = ref(false)

// ── Availability ──
const availItems = ref<AircraftAvailabilityDto[]>([])
const loadingAvail = ref(false)
const availFormVisible = ref(false)
const savingAvail = ref(false)
const availFormError = ref('')
const availEditTarget = ref<AircraftAvailabilityDto | null>(null)
const availForm = reactive({
  startDateTime: '',
  endDateTime: '',
  availabilityType: 'Available',
  reason: '',
})

// ── Insurance ──
const insItems = ref<InsurancePolicyDto[]>([])
const loadingIns = ref(false)
const insFormVisible = ref(false)
const savingIns = ref(false)
const insFormError = ref('')
const insEditTarget = ref<InsurancePolicyDto | null>(null)
const insForm = reactive({
  policyNumber: '',
  insuranceProvider: '',
  coverageAmount: '',
  coverageType: '',
  startDate: '',
  endDate: '',
})

// ── Helpers ──
function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function detailStatusLabel(ac: AircraftDto): string {
  switch (ac.status) {
    case 'InsuranceInactive': return 'Insurance Inactive'
    case 'Maintenance': return 'Maintenance'
    case 'Unavailable': return 'Unavailable'
    case 'Available': return 'Available'
    default: return ac.isAvailable ? 'Available' : 'Unavailable'
  }
}

function detailStatusBadgeClass(ac: AircraftDto): string {
  switch (ac.status) {
    case 'InsuranceInactive': return 'bg-amber-100 text-amber-800'
    case 'Maintenance': return 'bg-blue-100 text-blue-800'
    case 'Unavailable': return 'bg-red-100 text-red-800'
    case 'Available': return 'bg-emerald-100 text-emerald-800'
    default: return ac.isAvailable ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800'
  }
}

function availBadgeClass(type: string): string {
  switch (type.toLowerCase()) {
    case 'available': return 'bg-emerald-100 text-emerald-800'
    case 'blocked': return 'bg-red-100 text-red-800'
    case 'maintenance': return 'bg-amber-100 text-amber-800'
    default: return 'bg-slate-100 text-slate-600'
  }
}

// ── Load functions ──
async function loadAircraft() {
  loadingAircraft.value = true
  aircraftError.value = ''
  try {
    aircraft.value = await aircraftService.getById(aircraftId)
  } catch (err: unknown) {
    aircraftError.value = err instanceof Error ? err.message : 'Failed to load aircraft'
  } finally {
    loadingAircraft.value = false
  }
}

async function loadPhotos() {
  loadingPhotos.value = true
  try {
    photos.value = await aircraftPhotoService.getAll(aircraftId)
  } catch {
    photos.value = []
  } finally {
    loadingPhotos.value = false
  }
}

async function loadAvailability() {
  loadingAvail.value = true
  try {
    availItems.value = await availabilityService.getAll(aircraftId)
  } catch {
    availItems.value = []
  } finally {
    loadingAvail.value = false
  }
}

async function loadInsurance() {
  loadingIns.value = true
  try {
    insItems.value = await insuranceService.getAll(aircraftId)
  } catch {
    insItems.value = []
  } finally {
    loadingIns.value = false
  }
}

// ── Photo actions ──
async function handlePhotoUpload(e: Event) {
  const input = e.target as HTMLInputElement
  if (!input.files?.length) return
  const formData = new FormData()
  formData.append('file', input.files[0])
  try {
    await aircraftPhotoService.upload(aircraftId, formData)
    await loadPhotos()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Upload failed')
  }
  input.value = ''
}

async function setPhotoPrimary(photo: AircraftPhotoDto) {
  try {
    await aircraftPhotoService.setPrimary(aircraftId, photo.id)
    await loadPhotos()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Failed to set primary')
  }
}

async function deletePhoto(photo: AircraftPhotoDto) {
  if (!confirm('Delete this photo?')) return
  try {
    await aircraftPhotoService.delete(aircraftId, photo.id)
    await loadPhotos()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Delete failed')
  }
}

// ── Availability CRUD ──
function openAvailCreate() {
  availEditTarget.value = null
  Object.assign(availForm, { startDateTime: '', endDateTime: '', availabilityType: 'Available', reason: '' })
  availFormError.value = ''
  availFormVisible.value = true
}

function openAvailEdit(av: AircraftAvailabilityDto) {
  availEditTarget.value = av
  Object.assign(availForm, {
    startDateTime: av.startDateTime.substring(0, 16),
    endDateTime: av.endDateTime.substring(0, 16),
    availabilityType: av.availabilityType,
    reason: av.reason || '',
  })
  availFormError.value = ''
  availFormVisible.value = true
}

async function saveAvail() {
  if (!availForm.startDateTime || !availForm.endDateTime) {
    availFormError.value = 'Start and end date/time are required'
    return
  }
  savingAvail.value = true
  availFormError.value = ''
  const payload = {
    startDateTime: availForm.startDateTime,
    endDateTime: availForm.endDateTime,
    availabilityType: availForm.availabilityType,
    reason: availForm.reason || undefined,
  }
  try {
    if (availEditTarget.value) {
      await availabilityService.update(aircraftId, availEditTarget.value.id, { id: availEditTarget.value.id, ...payload })
    } else {
      await availabilityService.create(aircraftId, payload)
    }
    availFormVisible.value = false
    await loadAvailability()
  } catch (err: unknown) {
    availFormError.value = err instanceof Error ? err.message : 'Save failed'
  } finally {
    savingAvail.value = false
  }
}

async function deleteAvail(av: AircraftAvailabilityDto) {
  if (!confirm('Delete this availability block?')) return
  try {
    await availabilityService.delete(aircraftId, av.id)
    await loadAvailability()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Delete failed')
  }
}

// ── Insurance CRUD ──
function openInsCreate() {
  insEditTarget.value = null
  Object.assign(insForm, { policyNumber: '', insuranceProvider: '', coverageAmount: '', coverageType: '', startDate: '', endDate: '' })
  insFormError.value = ''
  insFormVisible.value = true
}

function openInsEdit(ins: InsurancePolicyDto) {
  insEditTarget.value = ins
  Object.assign(insForm, {
    policyNumber: ins.policyNumber,
    insuranceProvider: ins.insuranceProvider,
    coverageAmount: String(ins.coverageAmount),
    coverageType: ins.coverageType,
    startDate: ins.startDate.substring(0, 10),
    endDate: ins.endDate.substring(0, 10),
  })
  insFormError.value = ''
  insFormVisible.value = true
}

async function saveIns() {
  if (!insForm.policyNumber || !insForm.insuranceProvider || !insForm.startDate || !insForm.endDate) {
    insFormError.value = 'All fields are required'
    return
  }
  savingIns.value = true
  insFormError.value = ''
  const payload = {
    policyNumber: insForm.policyNumber,
    insuranceProvider: insForm.insuranceProvider,
    coverageAmount: Number(insForm.coverageAmount) || 0,
    coverageType: insForm.coverageType,
    startDate: insForm.startDate,
    endDate: insForm.endDate,
  }
  try {
    if (insEditTarget.value) {
      await insuranceService.update(aircraftId, insEditTarget.value.id, { id: insEditTarget.value.id, ...payload })
    } else {
      await insuranceService.create(aircraftId, payload)
    }
    insFormVisible.value = false
    await loadInsurance()
    await loadAvailability()
  } catch (err: unknown) {
    insFormError.value = err instanceof Error ? err.message : 'Save failed'
  } finally {
    savingIns.value = false
  }
}

async function deleteIns(ins: InsurancePolicyDto) {
  if (!confirm(`Delete policy ${ins.policyNumber}?`)) return
  try {
    await insuranceService.delete(aircraftId, ins.id)
    await loadInsurance()
    await loadAvailability()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Delete failed')
  }
}

// ── Init ──
onMounted(async () => {
  await loadAircraft()
  await Promise.all([loadPhotos(), loadAvailability(), loadInsurance()])
})
</script>
