<template>
  <!-- ProfileView is reused under both /client/profile and /admin/profile -->
  <div class="max-w-lg">
    <!-- Page Header -->
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-slate-900 mb-1 tracking-tight">My Profile</h1>
      <p class="text-base text-slate-500">Your account information</p>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex flex-col items-center gap-3 py-12 text-slate-500">
      <div class="w-8 h-8 border-[3px] border-slate-200 border-t-blue-500 rounded-full animate-spin"></div>
      <p>Loading…</p>
    </div>

    <!-- Content -->
    <div v-else class="bg-white border border-slate-200 rounded-xl p-6 shadow-sm">
      <!-- Avatar -->
      <div class="flex flex-col items-center text-center pb-4">
        <div class="w-[72px] h-[72px] rounded-full bg-gradient-to-br from-blue-500 to-blue-700 text-white flex items-center justify-center text-2xl font-bold mb-3 ring-4 ring-blue-500/10">
          {{ userInitials }}
        </div>
        <h2 class="text-xl font-semibold text-slate-900 mb-1">{{ fullName }}</h2>
        <span class="text-sm text-slate-500">{{ roleLabel }}</span>
      </div>

      <hr class="border-0 border-t border-slate-100 my-4" />

      <!-- Account details -->
      <dl class="flex flex-col gap-4">
        <div class="flex justify-between items-baseline gap-4">
          <dt class="text-sm text-slate-500 font-medium whitespace-nowrap shrink-0">First name</dt>
          <dd class="text-sm text-slate-800 font-medium text-right break-all">{{ firstName }}</dd>
        </div>
        <div class="flex justify-between items-baseline gap-4">
          <dt class="text-sm text-slate-500 font-medium whitespace-nowrap shrink-0">Last name</dt>
          <dd class="text-sm text-slate-800 font-medium text-right break-all">{{ lastName }}</dd>
        </div>
        <div class="flex justify-between items-baseline gap-4">
          <dt class="text-sm text-slate-500 font-medium whitespace-nowrap shrink-0">Email</dt>
          <dd class="text-sm text-slate-800 font-medium text-right break-all">{{ email }}</dd>
        </div>
        <div v-if="companyName" class="flex justify-between items-baseline gap-4">
          <dt class="text-sm text-slate-500 font-medium whitespace-nowrap shrink-0">Company</dt>
          <dd class="text-sm text-slate-800 font-medium text-right break-all">{{ companyName }}</dd>
        </div>
      </dl>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useSessionStore } from '@/stores/sessionStore'

const sessionStore = useSessionStore()

const loading = ref(true)

// All data comes from the session store — already populated at login
const firstName = computed(() => sessionStore.user?.firstName ?? '')
const lastName = computed(() => sessionStore.user?.lastName ?? '')
const fullName = computed(() => sessionStore.fullName || 'User')
const email = computed(() => sessionStore.user?.email ?? '')
const companyName = computed(() => sessionStore.activeCompany?.companyName ?? '')

const userInitials = computed(() => {
  const name = sessionStore.fullName
  if (!name) return 'U'
  return name
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2)
})

const roleLabel = computed(() => {
  const role = sessionStore.activeCompany?.role
  if (role === 'CompanyOwner') return 'Company Owner'
  return 'Pilot'
})

onMounted(() => {
  // Session data is already available — no API call needed
  loading.value = false
})
</script>
