<template>
  <div>
    <h1 class="text-2xl font-bold text-slate-900 m-0 mb-1 text-center tracking-tight">Create account</h1>
    <p class="text-sm text-slate-500 m-0 mb-6 text-center">Sign up to browse aircraft and book flights</p>

    <AppAlert v-if="errorMessage" type="error" class="mb-4">{{ errorMessage }}</AppAlert>
    <AppAlert v-if="successMessage" type="success" class="mb-4">{{ successMessage }}</AppAlert>

    <form class="flex flex-col gap-4" @submit.prevent="handleSubmit" novalidate>
      <div class="grid grid-cols-2 gap-3 max-[420px]:grid-cols-1">
        <AppInput
          id="firstName"
          v-model="form.firstName"
          label="First name"
          placeholder="Jane"
          required
          :error="errors.firstName"
        />
        <AppInput
          id="lastName"
          v-model="form.lastName"
          label="Last name"
          placeholder="Doe"
          required
          :error="errors.lastName"
        />
      </div>

      <AppInput
        id="email"
        v-model="form.email"
        label="Email address"
        type="text"
        placeholder="you@example.com"
        autocomplete="email"
        required
        :error="errors.email"
      />

      <AppInput
        id="password"
        v-model="form.password"
        label="Password"
        type="password"
        placeholder="At least 8 characters"
        autocomplete="new-password"
        required
        :error="errors.password"
      />

      <AppInput
        id="confirmPassword"
        v-model="form.confirmPassword"
        label="Confirm password"
        type="password"
        placeholder="Repeat password"
        autocomplete="new-password"
        required
        :error="errors.confirmPassword"
      />

      <AppButton type="submit" block :loading="loading">Create account</AppButton>
    </form>

    <p class="text-center text-sm text-slate-500 mt-6 mb-0">
      Already have an account?
      <RouterLink :to="{ name: 'login' }" class="text-blue-600 no-underline font-medium transition-colors duration-150 hover:text-blue-700 hover:underline">Sign in</RouterLink>
    </p>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppInput from '@/components/common/AppInput.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import { useAuthStore } from '@/stores/authStore'

const authStore = useAuthStore()
const router = useRouter()

const form = reactive({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: '',
})
const errors = reactive({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: '',
})
const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

function validate(): boolean {
  Object.keys(errors).forEach((k) => ((errors as Record<string, string>)[k] = ''))
  let ok = true
  if (!form.firstName.trim()) { errors.firstName = 'First name is required'; ok = false }
  if (!form.lastName.trim()) { errors.lastName = 'Last name is required'; ok = false }
  if (!form.email) { errors.email = 'Email is required'; ok = false }
  else if (!/^[^\s@]+@[^\s@]+$/.test(form.email)) { errors.email = 'Enter a valid email'; ok = false }
  if (!form.password) { errors.password = 'Password is required'; ok = false }
  else if (form.password.length < 8) { errors.password = 'Password must be at least 8 characters'; ok = false }
  if (form.confirmPassword !== form.password) { errors.confirmPassword = 'Passwords do not match'; ok = false }
  return ok
}

async function handleSubmit() {
  if (!validate()) return
  loading.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    await authStore.register({
      firstname: form.firstName,
      lastname: form.lastName,
      email: form.email,
      password: form.password,
    })
    await router.replace({ name: 'client-dashboard' })
  } catch (err: unknown) {
    errorMessage.value = err instanceof Error ? err.message : 'Registration failed. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>
