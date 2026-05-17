<template>
  <button
    :class="[baseClasses, variantClasses[variant], sizeClasses[size], { 'w-full': block, 'cursor-wait': loading }]"
    :disabled="disabled || loading"
    :type="type"
    v-bind="$attrs"
  >
    <span v-if="loading" class="inline-block w-[1em] h-[1em] border-2 border-current border-t-transparent rounded-full animate-spin flex-shrink-0" aria-hidden="true" />
    <slot />
  </button>
</template>

<script setup lang="ts">
interface Props {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md' | 'lg'
  type?: 'button' | 'submit' | 'reset'
  loading?: boolean
  disabled?: boolean
  block?: boolean
}

withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  type: 'button',
  loading: false,
  disabled: false,
  block: false,
})

const baseClasses = 'inline-flex items-center justify-center gap-2 font-semibold rounded-xl cursor-pointer border border-transparent transition-all duration-200 ease-out whitespace-nowrap no-underline leading-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500/50 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed'

const variantClasses: Record<string, string> = {
  primary: 'bg-gradient-to-b from-blue-600 to-blue-700 text-white shadow-btn hover:from-blue-700 hover:to-blue-800 hover:shadow-btn-hover hover:-translate-y-px active:translate-y-0 active:shadow-sm active:from-blue-800 active:to-blue-800',
  secondary: 'bg-white text-slate-700 border-slate-200 shadow-sm hover:bg-slate-50 hover:border-slate-300 hover:shadow-md hover:-translate-y-px active:translate-y-0',
  danger: 'bg-gradient-to-b from-red-500 to-red-600 text-white shadow-btn-danger hover:from-red-600 hover:to-red-700 hover:shadow-btn-danger-hover hover:-translate-y-px active:translate-y-0 active:shadow-sm focus-visible:ring-red-500/50',
  ghost: 'bg-transparent text-slate-600 border-transparent hover:bg-slate-100 hover:text-slate-800',
}

const sizeClasses: Record<string, string> = {
  sm: 'py-1.5 px-3.5 text-xs',
  md: 'py-2.5 px-5 text-sm',
  lg: 'py-3 px-7 text-base',
}
</script>
