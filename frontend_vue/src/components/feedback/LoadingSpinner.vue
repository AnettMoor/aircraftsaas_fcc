<template>
  <div
    :class="[
      'flex flex-col items-center justify-center gap-3 p-8',
      fullPage ? 'fixed inset-0 bg-white/85 backdrop-blur-sm z-[9999]' : '',
    ]"
    role="status"
    :aria-label="label"
  >
    <div :class="['rounded-full border-slate-200 border-t-blue-600 animate-spin flex-shrink-0', spinnerSizeClasses[size]]" />
    <p v-if="text" class="text-sm text-slate-500 m-0 font-medium">{{ text }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  size?: 'sm' | 'md' | 'lg'
  text?: string
  fullPage?: boolean
  label?: string
}

withDefaults(defineProps<Props>(), {
  size: 'md',
  fullPage: false,
  label: 'Loading…',
})

const spinnerSizeClasses: Record<string, string> = {
  sm: 'w-5 h-5 border-2',
  md: 'w-8 h-8 border-[3px]',
  lg: 'w-12 h-12 border-4',
}
</script>
