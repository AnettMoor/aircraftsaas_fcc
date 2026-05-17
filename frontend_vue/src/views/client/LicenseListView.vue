<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4 mb-6">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">My Pilot Licenses</h1>
      </div>
      <AppButton @click="openCreate">+ Add license</AppButton>
    </div>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="error" :message="error" retryable @retry="loadLicenses" />

    <EmptyState
      v-else-if="items.length === 0"
      icon="📜"
      title="No licenses yet"
      description="Add your pilot licenses to keep track of their validity and expiry dates."
    >
      <template #action>
        <AppButton @click="openCreate">Add license</AppButton>
      </template>
    </EmptyState>

    <div v-else class="bg-white border border-slate-200 rounded-xl shadow-sm overflow-hidden">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-slate-200 bg-slate-50">
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">License Number</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Type</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Issuing Authority</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Issue Date</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Expiry Date</th>
            <th class="px-4 py-3 font-semibold text-slate-600 text-xs uppercase tracking-wider">Status</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          <tr v-for="lic in items" :key="lic.id" class="hover:bg-slate-50 transition-colors">
            <td class="px-4 py-3 font-mono text-xs text-slate-700">{{ lic.licenseNumber }}</td>
            <td class="px-4 py-3 text-slate-700">{{ lic.licenseType }}</td>
            <td class="px-4 py-3 text-slate-700">{{ lic.issuingAuthority }}</td>
            <td class="px-4 py-3 text-slate-700">{{ formatDate(lic.issueDate) }}</td>
            <td class="px-4 py-3 text-slate-700">{{ formatDate(lic.expiryDate) }}</td>
            <td class="px-4 py-3">
              <span :class="[
                'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold',
                isExpired(lic) ? 'bg-red-100 text-red-700' : 'bg-emerald-100 text-emerald-700'
              ]">
                {{ isExpired(lic) ? 'Expired' : 'Valid' }}
              </span>
            </td>
            <td class="px-4 py-3">
              <div class="flex gap-2">
                <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="openEdit(lic)">Edit</button>
                <button class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="confirmDelete(lic)">Delete</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Inline form panel -->
    <div v-if="formVisible" class="mt-6 bg-white border border-slate-200 rounded-xl shadow-sm p-6">
      <h2 class="text-lg font-semibold text-slate-900 mb-4">{{ editTarget ? 'Edit license' : 'Add license' }}</h2>
      <AppAlert v-if="formError" type="error" class="mb-4">{{ formError }}</AppAlert>
      <form @submit.prevent="saveLicense" novalidate class="space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.licenseNumber" label="License Number" required :error="formErrors.licenseNumber" />
          <AppSelect v-model="form.licenseType" label="License Type" required :error="formErrors.licenseType" :options="licenseTypeOptions" placeholder="— Select license type —" />
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.issuingAuthority" label="Issuing Authority" required :error="formErrors.issuingAuthority" />
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <AppInput v-model="form.issueDate" label="Issue Date" type="date" required :error="formErrors.issueDate" />
          <AppInput v-model="form.expiryDate" label="Expiry Date" type="date" required :error="formErrors.expiryDate" />
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
import { licenseService } from '@/api'
import type { LicenseDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import EmptyState from '@/components/feedback/EmptyState.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppSelect from '@/components/common/AppSelect.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const licenseTypeOptions = [
  { value: 'LAPL(A)', label: 'LAPL(A) — Light Aircraft Pilot License (Airplane)' },
  { value: 'LAPL(H)', label: 'LAPL(H) — Light Aircraft Pilot License (Helicopter)' },
  { value: 'PPL', label: 'PPL — Private Pilot License' },
  { value: 'CPL', label: 'CPL — Commercial Pilot License' },
  { value: 'ATPL', label: 'ATPL — Airline Transport Pilot License' },
]

const items = ref<LicenseDto[]>([])
const loading = ref(false)
const error = ref('')
const formVisible = ref(false)
const saving = ref(false)
const formError = ref('')
const editTarget = ref<LicenseDto | null>(null)

const form = reactive({
  licenseNumber: '',
  licenseType: '',
  issueDate: '',
  expiryDate: '',
  issuingAuthority: '',
})

const formErrors = reactive({
  licenseNumber: '',
  licenseType: '',
  issueDate: '',
  expiryDate: '',
  issuingAuthority: '',
})

function isExpired(lic: LicenseDto): boolean {
  if (!lic.isValid) return true
  return new Date(lic.expiryDate) < new Date()
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function resetForm() {
  Object.assign(form, { licenseNumber: '', licenseType: '', issueDate: '', expiryDate: '', issuingAuthority: '' })
  Object.keys(formErrors).forEach((k) => ((formErrors as Record<string, string>)[k] = ''))
  formError.value = ''
}

function openCreate() {
  editTarget.value = null
  resetForm()
  formVisible.value = true
}

function openEdit(lic: LicenseDto) {
  editTarget.value = lic
  Object.assign(form, {
    licenseNumber: lic.licenseNumber,
    licenseType: lic.licenseType,
    issueDate: lic.issueDate.substring(0, 10),
    expiryDate: lic.expiryDate.substring(0, 10),
    issuingAuthority: lic.issuingAuthority,
  })
  formError.value = ''
  formVisible.value = true
}

function validate(): boolean {
  let ok = true
  Object.keys(formErrors).forEach((k) => ((formErrors as Record<string, string>)[k] = ''))
  if (!form.licenseNumber) { formErrors.licenseNumber = 'Required'; ok = false }
  if (!form.licenseType) { formErrors.licenseType = 'Required'; ok = false }
  if (!form.issueDate) { formErrors.issueDate = 'Required'; ok = false }
  if (!form.expiryDate) { formErrors.expiryDate = 'Required'; ok = false }
  if (!form.issuingAuthority) { formErrors.issuingAuthority = 'Required'; ok = false }
  return ok
}

async function saveLicense() {
  if (!validate()) return
  saving.value = true
  formError.value = ''
  try {
    if (editTarget.value) {
      await licenseService.update(editTarget.value.id, {
        id: editTarget.value.id,
        licenseNumber: form.licenseNumber,
        licenseType: form.licenseType,
        issueDate: form.issueDate,
        expiryDate: form.expiryDate,
        issuingAuthority: form.issuingAuthority,
      })
    } else {
      await licenseService.create({
        licenseNumber: form.licenseNumber,
        licenseType: form.licenseType,
        issueDate: form.issueDate,
        expiryDate: form.expiryDate,
        issuingAuthority: form.issuingAuthority,
      })
    }
    formVisible.value = false
    await loadLicenses()
  } catch (err: unknown) {
    formError.value = err instanceof Error ? err.message : 'Save failed'
  } finally {
    saving.value = false
  }
}

async function confirmDelete(lic: LicenseDto) {
  if (!confirm(`Delete license ${lic.licenseNumber}?`)) return
  try {
    await licenseService.delete(lic.id)
    await loadLicenses()
  } catch (err: unknown) {
    alert(err instanceof Error ? err.message : 'Delete failed')
  }
}

async function loadLicenses() {
  loading.value = true
  error.value = ''
  try {
    items.value = await licenseService.getAll()
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load licenses'
  } finally {
    loading.value = false
  }
}

onMounted(loadLicenses)
</script>
