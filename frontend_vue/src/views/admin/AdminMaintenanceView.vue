<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4 mb-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">Maintenance Records</h1>
      </div>
      <AppButton @click="openCreate">+ Add record</AppButton>
    </div>

    <!-- Aircraft filter -->
    <div class="flex items-center gap-3 mb-6">
      <label class="text-sm font-medium text-slate-600 whitespace-nowrap">Filter by aircraft:</label>
      <select
        v-model="filterAircraftId"
        class="px-3 py-2 border border-slate-200 rounded-lg text-sm text-slate-700 bg-white transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/10"
        @change="loadRecords"
      >
        <option value="">All aircraft</option>
        <option v-for="ac in aircraft" :key="ac.id" :value="ac.id">
          {{ ac.registrationNumber }} — {{ ac.make }} {{ ac.model }}
        </option>
      </select>
    </div>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadRecords" />

    <EmptyState
      v-else-if="items.length === 0"
      icon="🔧"
      title="No maintenance records"
      description="Create a maintenance record to track service history for your aircraft."
    >
      <template #action>
        <AppButton @click="openCreate">Add record</AppButton>
      </template>
    </EmptyState>

    <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-slate-200 bg-slate-50">
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Aircraft</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Timeframe</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Type</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Status</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Performed By</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Cost</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Next Due</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          <tr v-for="rec in items" :key="rec.id" class="hover:bg-slate-50 transition-colors">
            <td class="px-4 py-3 text-slate-700">{{ rec.aircraftName }}</td>
            <td class="px-4 py-3 text-slate-700">
              <template v-if="rec.startDate && rec.endDate">
                {{ formatDateTime(rec.startDate) }} — {{ formatDateTime(rec.endDate) }}
              </template>
              <template v-else>
                {{ formatDate(rec.maintenanceDate) }}
              </template>
            </td>
            <td class="px-4 py-3 text-slate-700">{{ rec.maintenanceType }}</td>
            <td class="px-4 py-3">
              <span :class="statusBadgeClass(rec)">
                {{ rec.isCompleted ? 'Completed' : (rec.status || 'Scheduled') }}
              </span>
            </td>
            <td class="px-4 py-3 text-slate-700">{{ rec.performedBy || '—' }}</td>
            <td class="px-4 py-3 text-slate-700">{{ rec.cost ? `€${rec.cost.toFixed(2)}` : '—' }}</td>
            <td class="px-4 py-3 text-slate-700">{{ rec.nextDueDate ? formatDate(rec.nextDueDate) : '—' }}</td>
            <td class="px-4 py-3">
              <div class="flex gap-2">
                <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="openEdit(rec)">Edit</button>
                <button class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="confirmDelete(rec)">Delete</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Inline form panel -->
    <div v-if="formVisible" class="mt-6 bg-white border border-slate-200 rounded-xl shadow-sm p-6">
      <h2 class="text-lg font-semibold text-slate-900 mb-4">{{ editTarget ? 'Edit record' : 'Add maintenance record' }}</h2>
      <AppAlert v-if="formError" type="error" class="mb-4">{{ formError }}</AppAlert>
      <form @submit.prevent="saveRecord" novalidate class="space-y-4">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div class="flex flex-col gap-1">
            <label class="text-sm font-medium text-slate-700">Aircraft *</label>
            <select
              v-model="form.aircraftId"
              :class="[
                'px-3 py-2 border rounded-lg text-base text-slate-700 bg-white min-h-[2.5rem] transition-all focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15',
                formErrors.aircraftId ? 'border-red-500' : 'border-slate-200'
              ]"
            >
              <option value="">Select aircraft</option>
              <option v-for="ac in aircraft" :key="ac.id" :value="ac.id">
                {{ ac.registrationNumber }} — {{ ac.make }} {{ ac.model }}
              </option>
            </select>
            <span v-if="formErrors.aircraftId" class="text-xs text-red-500">{{ formErrors.aircraftId }}</span>
          </div>
          <AppInput v-model="form.maintenanceType" label="Maintenance Type" required :error="formErrors.maintenanceType" />
        </div>

        <!-- From-Until Timeframe -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <AppInput v-model="form.startDate" label="Start Date & Time *" type="datetime-local" required :error="formErrors.startDate" />
          <AppInput v-model="form.endDate" label="End Date & Time *" type="datetime-local" required :error="formErrors.endDate" />
        </div>
        <p class="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
          ⚠️ The aircraft will be <strong>unavailable for booking</strong> during this maintenance timeframe.
        </p>

        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <AppInput v-model="form.performedBy" label="Performed By" />
          <AppInput v-model="form.description" label="Description" />
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <AppInput v-model="form.airframeHoursAtMaintenance" label="Airframe Hours" type="number" />
          <AppInput v-model="form.cost" label="Cost (€)" type="number" />
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <AppInput v-model="form.nextDueDate" label="Next Due Date" type="date" />
          <AppInput v-model="form.nextDueHours" label="Next Due Hours" type="number" />
        </div>
        <div>
          <label class="text-sm font-medium text-slate-700 flex items-center gap-2">
            <input type="checkbox" v-model="form.isCompleted" class="rounded border-slate-300" />
            Completed
          </label>
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
import { ref, reactive, onMounted } from 'vue'
import { aircraftService, maintenanceService } from '@/api'
import type { AircraftDto, MaintenanceRecordDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const badgeBase = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold'

const items = ref<MaintenanceRecordDto[]>([])
const aircraft = ref<AircraftDto[]>([])
const loading = ref(false)
const error = ref('')
const formVisible = ref(false)
const saving = ref(false)
const formError = ref('')
const editTarget = ref<MaintenanceRecordDto | null>(null)
const filterAircraftId = ref('')

const form = reactive({
  aircraftId: '',
  startDate: '',
  endDate: '',
  maintenanceType: '',
  description: '',
  performedBy: '',
  airframeHoursAtMaintenance: '',
  nextDueDate: '',
  nextDueHours: '',
  cost: '',
  isCompleted: false,
})

const formErrors = reactive({
  aircraftId: '',
  maintenanceType: '',
  startDate: '',
  endDate: '',
})

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function formatDateTime(iso: string) {
  const d = new Date(iso)
  const date = d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
  const time = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
  return `${date} ${time}`
}

function statusBadgeClass(rec: MaintenanceRecordDto) {
  const s = (rec.status || '').toLowerCase()
  if (s === 'completed' || rec.isCompleted) return `${badgeBase} bg-emerald-100 text-emerald-700`
  if (s === 'inprogress' || s === 'in progress') return `${badgeBase} bg-amber-100 text-amber-700`
  if (s === 'cancelled') return `${badgeBase} bg-red-100 text-red-700`
  return `${badgeBase} bg-slate-100 text-slate-600`
}

function resetForm() {
  Object.assign(form, {
    aircraftId: '', startDate: '', endDate: '', maintenanceType: '', description: '',
    performedBy: '', airframeHoursAtMaintenance: '', nextDueDate: '', nextDueHours: '',
    cost: '', isCompleted: false,
  })
  Object.keys(formErrors).forEach((k) => ((formErrors as Record<string, string>)[k] = ''))
  formError.value = ''
}

function openCreate() {
  editTarget.value = null
  resetForm()
  formVisible.value = true
}

function openEdit(rec: MaintenanceRecordDto) {
  editTarget.value = rec
  Object.assign(form, {
    aircraftId: rec.aircraftId,
    startDate: rec.startDate ? rec.startDate.substring(0, 16) : rec.maintenanceDate.substring(0, 16),
    endDate: rec.endDate ? rec.endDate.substring(0, 16) : rec.maintenanceDate.substring(0, 16),
    maintenanceType: rec.maintenanceType,
    description: rec.description || '',
    performedBy: rec.performedBy || '',
    airframeHoursAtMaintenance: String(rec.airframeHoursAtMaintenance),
    nextDueDate: rec.nextDueDate ? rec.nextDueDate.substring(0, 10) : '',
    nextDueHours: rec.nextDueHours != null ? String(rec.nextDueHours) : '',
    cost: rec.cost ? String(rec.cost) : '',
    isCompleted: rec.isCompleted,
  })
  formError.value = ''
  formVisible.value = true
}

function validate(): boolean {
  let ok = true
  Object.keys(formErrors).forEach((k) => ((formErrors as Record<string, string>)[k] = ''))
  if (!form.aircraftId) { formErrors.aircraftId = 'Required'; ok = false }
  if (!form.maintenanceType) { formErrors.maintenanceType = 'Required'; ok = false }
  if (!form.startDate) { formErrors.startDate = 'Start date is required'; ok = false }
  if (!form.endDate) { formErrors.endDate = 'End date is required'; ok = false }
  if (form.startDate && form.endDate && form.startDate >= form.endDate) {
    formErrors.endDate = 'End date/time must be after start date/time'
    ok = false
  }
  return ok
}

async function saveRecord() {
  if (!validate()) return
  saving.value = true
  formError.value = ''
  const payload = {
    aircraftId: form.aircraftId,
    maintenanceDate: form.startDate,
    startDate: form.startDate || undefined,
    endDate: form.endDate || undefined,
    maintenanceType: form.maintenanceType,
    description: form.description || undefined,
    performedBy: form.performedBy || undefined,
    airframeHoursAtMaintenance: form.airframeHoursAtMaintenance ? Number(form.airframeHoursAtMaintenance) : 0,
    nextDueDate: form.nextDueDate || undefined,
    nextDueHours: form.nextDueHours ? Number(form.nextDueHours) : undefined,
    cost: form.cost ? Number(form.cost) : 0,
    isCompleted: form.isCompleted,
  }
  try {
    if (editTarget.value) {
      await maintenanceService.update(editTarget.value.id, { id: editTarget.value.id, ...payload })
    } else {
      await maintenanceService.create(payload)
    }
    formVisible.value = false
    await loadRecords()
  } catch (err: unknown) {
    formError.value = err instanceof Error ? err.message : 'Save failed'
  } finally {
    saving.value = false
  }
}

async function confirmDelete(rec: MaintenanceRecordDto) {
  const label = rec.startDate && rec.endDate
    ? `${formatDateTime(rec.startDate)} — ${formatDateTime(rec.endDate)}`
    : formatDate(rec.maintenanceDate)
  if (!confirm(`Delete maintenance record (${label})?`)) return
  try {
    await maintenanceService.delete(rec.id)
    await loadRecords()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Delete failed')
  }
}

async function loadRecords() {
  loading.value = true
  error.value = ''
  try {
    items.value = await maintenanceService.getAll(filterAircraftId.value || undefined)
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load maintenance records'
  } finally {
    loading.value = false
  }
}

async function loadAircraft() {
  try {
    aircraft.value = await aircraftService.getCompanyAircraft()
  } catch {
    // Silently fail — aircraft dropdown will be empty
  }
}

onMounted(async () => {
  await loadAircraft()
  await loadRecords()
})
</script>
