<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4 mb-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Aircraft Management</h1>
        <p class="text-base text-slate-500">Manage your company's aircraft fleet</p>
      </div>
      <AppButton @click="openCreate">+ Add aircraft</AppButton>
    </div>

    <!-- Success/Error Alerts -->
    <AppAlert v-if="successMsg" type="success" class="mb-4" dismissible>{{ successMsg }}</AppAlert>
    <AppAlert v-if="errorMsg" type="error" class="mb-4" dismissible>{{ errorMsg }}</AppAlert>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadAll" />

    <template v-else>
      <!-- Tabs -->
      <div class="flex gap-1 border-b border-slate-200 mb-6 overflow-x-auto">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          :class="[
            'px-4 py-2 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
            activeTab === tab.key
              ? 'border-blue-600 text-blue-600'
              : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
          ]"
          @click="activeTab = tab.key"
        >
          {{ tab.label }}
          <span :class="['ml-1.5 inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-semibold', tab.badgeClass]">{{ tab.count }}</span>
        </button>
      </div>

      <!-- All Aircraft Tab -->
      <div v-if="activeTab === 'all'" class="min-h-[200px]">
        <div v-if="items.length === 0" class="py-8">
          <EmptyState icon="✈" title="No aircraft yet" description="Add your first aircraft to start accepting bookings.">
            <template #action>
              <AppButton @click="openCreate">Add aircraft</AppButton>
            </template>
          </EmptyState>
        </div>
        <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <AircraftTable :aircraft="items" @edit="openEdit" @deactivate="confirmDeactivate" />
        </div>
      </div>

      <!-- Active Aircraft Tab -->
      <div v-if="activeTab === 'active'" class="min-h-[200px]">
        <div v-if="activeAircraft.length === 0" class="py-8">
          <EmptyState icon="✈" title="No Active Aircraft" description="No aircraft are currently marked as available." />
        </div>
        <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <AircraftTable :aircraft="activeAircraft" @edit="openEdit" @deactivate="confirmDeactivate" />
        </div>
      </div>

      <!-- Unavailable Aircraft Tab -->
      <div v-if="activeTab === 'unavailable'" class="min-h-[200px]">
        <div v-if="unavailableAircraft.length === 0" class="py-8">
          <EmptyState icon="✅" title="All Aircraft Available" description="All aircraft are currently marked as available." />
        </div>
        <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
          <AircraftTable :aircraft="unavailableAircraft" @edit="openEdit" @deactivate="confirmDeactivate" />
        </div>
      </div>

      <!-- Deactivated Aircraft Tab -->
      <div v-if="activeTab === 'deactivated'" class="min-h-[200px]">
        <div v-if="deletedItems.length === 0" class="py-8">
          <EmptyState icon="🔄" title="No Deactivated Aircraft" description="No aircraft have been deactivated." />
        </div>
        <div v-else>
          <div class="text-sm text-slate-500 bg-slate-50 border border-slate-200 rounded-lg p-3 mb-4">
            These aircraft have been removed from your fleet. You can reactivate them below.
          </div>
          <div class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
            <table class="w-full text-left text-sm">
              <thead>
                <tr class="border-b border-slate-200 bg-slate-50">
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Registration</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Aircraft</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Year</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Category</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Hours</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Rate</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Base</th>
                  <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Rating</th>
                  <th class="px-4 py-3"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr v-for="ac in deletedItems" :key="ac.id" class="opacity-60">
                  <td class="px-4 py-3 font-mono text-xs text-slate-500">{{ ac.registrationNumber }}</td>
                  <td class="px-4 py-3">
                    <div class="text-slate-500">{{ ac.make }}</div>
                    <small class="text-slate-400">{{ ac.model }}</small>
                  </td>
                  <td class="px-4 py-3 text-slate-500">{{ ac.year }}</td>
                  <td class="px-4 py-3">
                    <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 text-slate-600">{{ ac.category }}</span>
                  </td>
                  <td class="px-4 py-3 text-slate-500">{{ ac.totalAirspeedHours }} hrs</td>
                  <td class="px-4 py-3 text-slate-500">€{{ ac.hourlyRate?.toFixed(2) }}<small>/hr</small></td>
                  <td class="px-4 py-3 text-slate-500">{{ ac.baseAirportName }}</td>
                  <td class="px-4 py-3">
                    <div v-if="ac.reviewCount > 0" class="flex items-center gap-1 text-sm">
                      <span class="text-amber-400">★</span>
                      <span>{{ ac.averageRating?.toFixed(1) }}</span>
                      <small class="text-slate-400">({{ ac.reviewCount }})</small>
                    </div>
                    <span v-else class="text-slate-400 text-sm">No reviews</span>
                  </td>
                  <td class="px-4 py-3">
                    <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="confirmReactivate(ac)">
                      🔄 Reactivate
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </template>

    <!-- Deactivate Confirmation Modal -->
    <div v-if="deactivateTarget" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50" @click.self="deactivateTarget = null">
      <div class="bg-white rounded-xl shadow-lg w-full max-w-md mx-4 animate-modal-enter">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200">
          <h3 class="text-lg font-semibold text-slate-900">Deactivate Aircraft</h3>
          <button class="text-slate-400 hover:text-slate-600 text-2xl leading-none transition-colors" @click="deactivateTarget = null">✕</button>
        </div>
        <div class="px-6 py-4">
          <p class="text-base text-slate-700">Are you sure you want to deactivate <strong>{{ deactivateTarget.registrationNumber }}</strong>?</p>
          <p class="text-sm text-slate-500">{{ deactivateTarget.make }} {{ deactivateTarget.model }} ({{ deactivateTarget.year }})</p>
          <div class="mt-3 text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-3">
            ⚠ The aircraft will be moved to the Deactivated section. You can reactivate it later.
          </div>
        </div>
        <div class="flex justify-end gap-3 px-6 py-4 border-t border-slate-200">
          <AppButton variant="secondary" @click="deactivateTarget = null">Cancel</AppButton>
          <AppButton variant="danger" :loading="deactivating" @click="doDeactivate">Deactivate</AppButton>
        </div>
      </div>
    </div>

    <!-- Simple inline form panel -->
    <div v-if="formVisible" class="mt-6 bg-white border border-slate-200 rounded-xl shadow-sm p-6">
      <h2 class="text-lg font-semibold text-slate-900 mb-4">{{ editTarget ? 'Edit aircraft' : 'Add aircraft' }}</h2>
      <AppAlert v-if="formError" type="error" class="mb-4">{{ formError }}</AppAlert>
      <form @submit.prevent="saveAircraft" novalidate class="space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.registrationNumber" label="Registration" required :error="formErrors.registrationNumber" />
          <AppInput v-model="form.make" label="Make" required :error="formErrors.make" />
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.model" label="Model" required :error="formErrors.model" />
          <AppInput v-model="form.year" label="Year" type="number" required :error="formErrors.year" />
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.category" label="Category" required :error="formErrors.category" />
          <AppSelect v-model="form.requiredLicenseType" label="Required License Type" required :error="formErrors.requiredLicenseType" :options="licenseTypeOptions" placeholder="— Select license type —" />
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.hourlyRate" label="Hourly rate (€)" type="number" required :error="formErrors.hourlyRate" />
          <AppInput v-model="form.totalAirspeedHours" label="Total airspeed hours" type="number" />
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppSelect v-model="form.baseAirportId" label="Base Airport" required :error="formErrors.baseAirportId" :options="airportOptions" placeholder="— Select airport —" />
        </div>
        <div>
          <AppInput v-model="form.description" label="Description" required :error="formErrors.description" />
        </div>
        <div class="flex justify-end gap-3 pt-2">
          <AppButton variant="secondary" type="button" @click="formVisible = false">Cancel</AppButton>
          <AppButton type="submit" :loading="saving">{{ editTarget ? 'Save changes' : 'Create' }}</AppButton>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { aircraftService, airportService } from '@/api'
import type { AircraftDto } from '@/types'
import type { AirportDto } from '@/types/airport'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppSelect from '@/components/common/AppSelect.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import AircraftTable from '@/components/admin/AircraftTable.vue'

const licenseTypeOptions = [
  { value: 'LAPL(A)', label: 'LAPL(A) — Light Aircraft Pilot License (Airplane)' },
  { value: 'LAPL(H)', label: 'LAPL(H) — Light Aircraft Pilot License (Helicopter)' },
  { value: 'PPL', label: 'PPL — Private Pilot License' },
  { value: 'CPL', label: 'CPL — Commercial Pilot License' },
  { value: 'ATPL', label: 'ATPL — Airline Transport Pilot License' },
]

const tabBadgeMap: Record<string, string> = {
  secondary: 'bg-slate-100 text-slate-600',
  success: 'bg-emerald-100 text-emerald-700',
  warning: 'bg-amber-100 text-amber-700',
  danger: 'bg-red-100 text-red-700',
}

// ── State ──────────────────────────────────────────────
const items = ref<AircraftDto[]>([])
const deletedItems = ref<AircraftDto[]>([])
const airports = ref<AirportDto[]>([])
const loading = ref(false)
const error = ref('')
const formVisible = ref(false)
const saving = ref(false)
const formError = ref('')
const editTarget = ref<AircraftDto | null>(null)
const successMsg = ref('')
const errorMsg = ref('')
const activeTab = ref<'all' | 'active' | 'unavailable' | 'deactivated'>('all')
const deactivateTarget = ref<AircraftDto | null>(null)
const deactivating = ref(false)

// ── Computed ──────────────────────────────────────────
const activeAircraft = computed(() => items.value.filter(a => a.status === 'Available'))
const unavailableAircraft = computed(() => items.value.filter(a => a.status !== 'Available'))

const tabs = computed(() => [
  { key: 'all' as const, label: 'All Aircraft', count: items.value.length, badgeClass: tabBadgeMap.secondary },
  { key: 'active' as const, label: 'Active', count: activeAircraft.value.length, badgeClass: tabBadgeMap.success },
  { key: 'unavailable' as const, label: 'Unavailable', count: unavailableAircraft.value.length, badgeClass: tabBadgeMap.warning },
  { key: 'deactivated' as const, label: 'Deactivated', count: deletedItems.value.length, badgeClass: tabBadgeMap.danger },
])

const airportOptions = computed(() =>
  airports.value.map((a) => ({
    value: a.id,
    label: `${a.icaoCode} / ${a.iataCode} — ${a.name} (${a.city})`,
  }))
)

// ── Form ──────────────────────────────────────────────
const form = reactive({
  registrationNumber: '', make: '', model: '', year: '',
  category: '', requiredLicenseType: '', totalAirspeedHours: '', hourlyRate: '', baseAirportId: '', description: '',
})
const formErrors = reactive({
  registrationNumber: '', make: '', model: '', year: '', category: '', requiredLicenseType: '',
  hourlyRate: '', baseAirportId: '', description: '',
})

function resetForm() {
  Object.assign(form, { registrationNumber: '', make: '', model: '', year: '', category: '', requiredLicenseType: '', totalAirspeedHours: '', hourlyRate: '', baseAirportId: '', description: '' })
  Object.keys(formErrors).forEach((k) => ((formErrors as Record<string, string>)[k] = ''))
  formError.value = ''
}

function openCreate() {
  editTarget.value = null
  resetForm()
  formVisible.value = true
}

function openEdit(ac: AircraftDto) {
  editTarget.value = ac
  Object.assign(form, {
    registrationNumber: ac.registrationNumber,
    make: ac.make,
    model: ac.model,
    year: String(ac.year),
    category: ac.category,
    requiredLicenseType: ac.requiredLicenseType || 'PPL',
    totalAirspeedHours: String(ac.totalAirspeedHours),
    hourlyRate: ac.hourlyRate ? String(ac.hourlyRate) : '',
    baseAirportId: ac.baseAirportId,
    description: ac.description,
  })
  formError.value = ''
  formVisible.value = true
}

function validate(): boolean {
  let ok = true
  Object.keys(formErrors).forEach((k) => ((formErrors as Record<string, string>)[k] = ''))
  if (!form.registrationNumber) { formErrors.registrationNumber = 'Required'; ok = false }
  if (!form.make) { formErrors.make = 'Required'; ok = false }
  if (!form.model) { formErrors.model = 'Required'; ok = false }
  if (!form.year) { formErrors.year = 'Required'; ok = false }
  if (!form.category) { formErrors.category = 'Required'; ok = false }
  if (!form.requiredLicenseType) { formErrors.requiredLicenseType = 'Required'; ok = false }
  if (!form.hourlyRate || Number(form.hourlyRate) < 0.01) { formErrors.hourlyRate = 'Minimum rate is €0.01'; ok = false }
  if (!form.baseAirportId) { formErrors.baseAirportId = 'Required'; ok = false }
  if (!form.description) { formErrors.description = 'Required'; ok = false }
  return ok
}

async function saveAircraft() {
  if (!validate()) return
  saving.value = true
  formError.value = ''
  const basePayload = {
    registrationNumber: form.registrationNumber,
    make: form.make,
    model: form.model,
    year: Number(form.year),
    category: form.category,
    requiredLicenseType: form.requiredLicenseType,
    totalAirspeedHours: form.totalAirspeedHours ? Number(form.totalAirspeedHours) : 0,
    hourlyRate: Number(form.hourlyRate),
    baseAirportId: form.baseAirportId,
    description: form.description,
  }
  try {
    if (editTarget.value) {
      await aircraftService.update(editTarget.value.id, { ...basePayload, id: editTarget.value.id, isAvailable: editTarget.value.isAvailable })
    } else {
      await aircraftService.create(basePayload)
    }
    formVisible.value = false
    successMsg.value = editTarget.value ? 'Aircraft updated successfully.' : 'Aircraft created successfully.'
    await loadAll()
  } catch (err: unknown) {
    formError.value = err instanceof Error ? err.message : 'Save failed'
  } finally {
    saving.value = false
  }
}

// ── Deactivate (soft-delete) ───────────────────────────
function confirmDeactivate(ac: AircraftDto) {
  deactivateTarget.value = ac
}

async function doDeactivate() {
  if (!deactivateTarget.value) return
  deactivating.value = true
  try {
    await aircraftService.delete(deactivateTarget.value.id)
    successMsg.value = `Aircraft ${deactivateTarget.value.registrationNumber} deactivated successfully.`
    deactivateTarget.value = null
    await loadAll()
  } catch (err: unknown) {
    errorMsg.value = err instanceof Error ? err.message : 'Deactivation failed'
    deactivateTarget.value = null
  } finally {
    deactivating.value = false
  }
}

// ── Reactivate (restore soft-deleted) ──────────────────
async function confirmReactivate(ac: AircraftDto) {
  if (!confirm(`Reactivate aircraft ${ac.registrationNumber}?`)) return
  try {
    await aircraftService.restore(ac.id)
    successMsg.value = `Aircraft ${ac.registrationNumber} reactivated successfully.`
    await loadAll()
  } catch (err: unknown) {
    errorMsg.value = err instanceof Error ? err.message : 'Reactivation failed'
  }
}

// ── Data loading ──────────────────────────────────────
async function loadAll() {
  loading.value = true
  error.value = ''
  try {
    const [active, deleted] = await Promise.all([
      aircraftService.getCompanyAircraft(),
      aircraftService.getCompanyDeletedAircraft().catch(() => [] as AircraftDto[]),
    ])
    items.value = active
    deletedItems.value = deleted
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load aircraft'
  } finally {
    loading.value = false
  }
}

async function loadAirports() {
  try {
    airports.value = await airportService.getAll()
  } catch (err: unknown) {
    console.error('Failed to load airports', err)
  }
}

onMounted(() => {
  loadAll()
  loadAirports()
})
</script>
