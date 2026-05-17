<template>
  <div>
    <h1 class="text-2xl font-bold tracking-tight text-slate-900 mb-6">Company Settings</h1>

    <LoadingSpinner v-if="loading" />
    <ErrorState v-else-if="loadError" :message="loadError" retryable @retry="loadCompany" />

    <template v-else-if="company">
      <AppAlert v-if="successMsg" type="success" class="mb-4" dismissible>{{ successMsg }}</AppAlert>
      <AppAlert v-if="saveError" type="error" class="mb-4">{{ saveError }}</AppAlert>

      <form @submit.prevent="save" novalidate class="flex flex-col">
        <div class="bg-white border border-slate-200 rounded-xl p-6 mb-4 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4 pb-3 border-b border-slate-100">General</h2>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
            <AppInput v-model="form.companyName" label="Company name" required :error="formErrors.companyName" />
          </div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-6 mb-4 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-900 mb-4 pb-3 border-b border-slate-100">Contact</h2>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
            <AppInput v-model="form.email" label="Email" type="email" :error="formErrors.email" />
            <AppInput v-model="form.phone" label="Phone" type="tel" />
          </div>
          <AppInput v-model="form.address" label="Address" />
        </div>

        <div class="flex justify-end mt-4">
          <AppButton type="submit" :loading="saving">Save changes</AppButton>
        </div>
      </form>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { companyService } from '@/api'
import type { CompanyDto } from '@/types'
import LoadingSpinner from '@/components/feedback/LoadingSpinner.vue'
import ErrorState from '@/components/feedback/ErrorState.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppInput from '@/components/common/AppInput.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import { useSessionStore } from '@/stores/sessionStore'

const sessionStore = useSessionStore()

const company = ref<CompanyDto | null>(null)
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const saveError = ref('')
const successMsg = ref('')

const form = reactive({
  companyName: '', email: '', phone: '', address: '',
})
const formErrors = reactive({ companyName: '', email: '' })

async function loadCompany() {
  const id = sessionStore.activeCompany?.companyId
  if (!id) return
  loading.value = true
  loadError.value = ''
  try {
    company.value = await companyService.getById(id)
    Object.assign(form, {
      companyName: company.value.companyName || '',
      email: company.value.email || '',
      phone: company.value.phone || '',
      address: company.value.address || '',
    })
  } catch (err: unknown) {
    loadError.value = err instanceof Error ? err.message : 'Failed to load company'
  } finally {
    loading.value = false
  }
}

async function save() {
  formErrors.companyName = ''
  formErrors.email = ''
  if (!form.companyName) { formErrors.companyName = 'Company name is required'; return }
  if (form.email && !/^[^\s@]+@[^\s@]+$/.test(form.email)) { formErrors.email = 'Invalid email'; return }

  saving.value = true
  saveError.value = ''
  successMsg.value = ''
  try {
    await companyService.update(company.value!.id, { ...form })
    successMsg.value = 'Company settings saved.'
  } catch (err: unknown) {
    saveError.value = err instanceof Error ? err.message : 'Save failed'
  } finally {
    saving.value = false
  }
}

onMounted(loadCompany)
</script>
