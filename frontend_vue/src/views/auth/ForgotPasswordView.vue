<template>
  <div>
    <h1 class="text-2xl font-bold text-slate-900 m-0 mb-1 text-center tracking-tight">Reset password</h1>
    <p class="text-sm text-slate-500 m-0 mb-6 text-center">Enter your email and we'll send reset instructions</p>

    <template v-if="!submitted">
      <AppAlert v-if="errorMessage" type="error" class="mb-4">{{ errorMessage }}</AppAlert>

      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit" novalidate>
        <AppInput
          id="email"
          v-model="email"
          label="Email address"
          type="email"
          placeholder="you@example.com"
          autocomplete="email"
          required
          :error="emailError"
        />
        <AppButton type="submit" block :loading="loading">Send reset link</AppButton>
      </form>
    </template>

    <AppAlert v-else type="success">
      If an account exists for <strong>{{ email }}</strong>, a reset link has been sent.
      Check your inbox.
    </AppAlert>

    <p class="text-center mt-6 mb-0">
      <RouterLink :to="{ name: 'login' }" class="text-sm text-blue-600 no-underline font-medium transition-colors duration-150 hover:text-blue-700 hover:underline">← Back to sign in</RouterLink>
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AppInput from '@/components/common/AppInput.vue'
import AppButton from '@/components/common/AppButton.vue'
import AppAlert from '@/components/common/AppAlert.vue'

const email = ref('')
const emailError = ref('')
const errorMessage = ref('')
const loading = ref(false)
const submitted = ref(false)

async function handleSubmit() {
  emailError.value = ''
  if (!email.value) { emailError.value = 'Email is required'; return }
  if (!/^[^\s@]+@[^\s@]+$/.test(email.value)) { emailError.value = 'Enter a valid email'; return }

  loading.value = true
  errorMessage.value = ''
  try {
    // TODO: connect to backend password reset endpoint when available
    // await authService.requestPasswordReset({ email: email.value })
    await new Promise((r) => setTimeout(r, 600)) // simulate
    submitted.value = true
  } catch {
    errorMessage.value = 'Could not send reset link. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>
