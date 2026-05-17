<template>
  <div
    v-if="visible"
    :class="['flex items-start gap-3 px-4 py-3 rounded-lg border text-sm', alertClasses[type]]"
    role="alert"
  >
    <span class="text-base flex-shrink-0 mt-px" aria-hidden="true">{{ icons[type] }}</span>
    <div class="flex-1">
      <p v-if="title" class="font-semibold m-0 mb-1">{{ title }}</p>
      <p class="m-0"><slot>{{ fallbackMessage }}</slot></p>
    </div>
    <button
      v-if="dismissible"
      class="bg-transparent border-none cursor-pointer opacity-60 text-sm p-1 text-current flex-shrink-0 rounded transition-all duration-150 hover:opacity-100 hover:bg-black/5"
      @click="visible = false"
      aria-label="Dismiss"
    >✕</button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface Props {
  type?: 'info' | 'success' | 'warning' | 'error'
  title?: string
  dismissible?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  type: 'info',
  dismissible: false,
})

const visible = ref(true)

const fallbackMessage = computed(() => {
  const fallbacks: Record<string, string> = {
    error: 'An error occurred. Please try again.',
    warning: 'Warning.',
    info: 'Information.',
    success: 'Operation completed successfully.',
  }
  return fallbacks[props.type] ?? 'An error occurred.'
})

const icons: Record<string, string> = {
  info: 'ℹ',
  success: '✓',
  warning: '⚠',
  error: '✕',
}

const alertClasses: Record<string, string> = {
  info: 'bg-blue-50 border-blue-300 text-blue-800',
  success: 'bg-emerald-50 border-emerald-300 text-emerald-800',
  warning: 'bg-amber-50 border-amber-300 text-amber-800',
  error: 'bg-red-50 border-red-300 text-red-800',
}
</script>
