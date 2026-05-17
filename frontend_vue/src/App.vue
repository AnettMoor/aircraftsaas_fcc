<template>
  <RouterView />

  <!-- Global toast container -->
  <Teleport to="body">
    <div class="fixed bottom-6 right-6 flex flex-col gap-3 z-[9999] max-w-[22rem]" aria-live="polite">
      <TransitionGroup name="toast">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          :class="['flex items-start gap-3 px-4 py-3 rounded-lg text-sm shadow-lg border backdrop-blur-sm', toastClasses[toast.type]]"
          role="alert"
        >
          <span class="text-base flex-shrink-0" aria-hidden="true">{{ toastIcons[toast.type] }}</span>
          <span class="flex-1 leading-snug">{{ toast.message }}</span>
          <button
            class="bg-transparent border-none cursor-pointer text-current opacity-60 text-sm p-0 flex-shrink-0 transition-opacity duration-150 hover:opacity-100"
            @click="removeToast(toast.id)"
            aria-label="Close"
          >✕</button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { useToast } from '@/composables/useToast'

const { toasts, removeToast } = useToast()

const toastIcons: Record<string, string> = {
  success: '✓',
  error: '✕',
  warning: '⚠',
  info: 'ℹ',
}

const toastClasses: Record<string, string> = {
  success: 'bg-emerald-50 border-emerald-300 text-emerald-800',
  error: 'bg-red-50 border-red-300 text-red-800',
  warning: 'bg-amber-50 border-amber-300 text-amber-800',
  info: 'bg-blue-50 border-blue-300 text-blue-800',
}
</script>

<style>
/* ─── Minimal global overrides (Tailwind base handles most) ─── */
h1, h2, h3, h4, h5, h6 {
  letter-spacing: -0.025em;
}

/* Toast transitions */
.toast-enter-active, .toast-leave-active { transition: all 300ms ease; }
.toast-enter-from { opacity: 0; transform: translateX(1rem); }
.toast-leave-to   { opacity: 0; transform: translateX(1rem); }
</style>
