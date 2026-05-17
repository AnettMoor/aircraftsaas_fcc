<template>
  <div>
    <h1 class="text-2xl font-bold text-slate-900 m-0 mb-1 text-center tracking-tight">Welcome back</h1>
    <p class="text-sm text-slate-500 m-0 mb-6 text-center">Sign in to your account</p>

    <AppAlert v-if="errorMessage" type="error" class="mb-4">{{ errorMessage }}</AppAlert>

    <form class="flex flex-col gap-4" @submit.prevent="handleSubmit" novalidate>
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
        placeholder="••••••••"
        autocomplete="current-password"
        required
        :error="errors.password"
      />

      <div class="flex justify-end">
        <RouterLink :to="{ name: 'forgot-password' }" class="text-sm text-blue-600 no-underline font-medium transition-colors duration-150 hover:text-blue-700 hover:underline">
          Forgot password?
        </RouterLink>
      </div>

      <AppButton type="submit" block :loading="loading">Sign in</AppButton>
    </form>

    <p class="text-center text-sm text-slate-500 mt-6 mb-0">
      Don't have an account?
      <RouterLink :to="{ name: 'register' }" class="text-blue-600 no-underline font-medium transition-colors duration-150 hover:text-blue-700 hover:underline">Create one</RouterLink>
    </p>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import AppInput from '@/components/common/AppInput.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppAlert from '@/components/common/AppAlert.vue'
import { useAuthStore } from '@/stores/authStore'
import { useSessionStore } from '@/stores/sessionStore'

const authStore = useAuthStore()
const sessionStore = useSessionStore()
const router = useRouter()
const route = useRoute()

const form = reactive({ email: '', password: '' })
const errors = reactive({ email: '', password: '' })
const loading = ref(false)
const errorMessage = ref('')

function validate(): boolean {
  errors.email = ''
  errors.password = ''
  let ok = true
  if (!form.email) { errors.email = 'Email is required'; ok = false }
  else if (!/^[^\s@]+@[^\s@]+$/.test(form.email)) { errors.email = 'Enter a valid email'; ok = false }
  if (!form.password) { errors.password = 'Password is required'; ok = false }
  return ok
}

async function handleSubmit() {
  if (!validate()) return
  loading.value = true
  errorMessage.value = ''
  try {
    await authStore.login({ email: form.email, password: form.password })
    const redirect = route.query.redirect as string | undefined
    const defaultRoute = sessionStore.isCompanyOwner
      ? { name: 'admin-dashboard' }
      : { name: 'client-dashboard' }
    await router.replace(redirect || defaultRoute)
  } catch (err: unknown) {
    errorMessage.value = err instanceof Error ? err.message : 'Login failed. Check your credentials.'
  } finally {
    loading.value = false
  }
}
</script>
