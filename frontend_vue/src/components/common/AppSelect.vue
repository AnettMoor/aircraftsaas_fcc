<template>
  <div class="flex flex-col gap-1">
    <label v-if="label" :for="selectId" class="text-xs font-medium text-slate-700">
      {{ label }}
      <span v-if="required" class="text-red-500 ml-0.5">*</span>
    </label>
    <div class="relative">
      <select
        :id="selectId"
        :class="[
          'w-full py-2 pl-3 pr-8 border rounded-lg text-sm text-slate-900 bg-white outline-none transition-all duration-150 appearance-none min-h-[2.5rem] cursor-pointer',
          'focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15',
          'disabled:bg-slate-50 disabled:text-slate-500 disabled:cursor-not-allowed',
          error ? 'border-red-500 focus:ring-red-500/15' : 'border-slate-300',
        ]"
        :value="modelValue"
        :disabled="disabled"
        :required="required"
        v-bind="$attrs"
        @change="$emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option v-for="opt in options" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
      <span class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none flex items-center" aria-hidden="true">
        <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
          <path d="M3 4.5L6 7.5L9 4.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </span>
    </div>
    <p v-if="error" class="text-xs text-red-500 m-0" role="alert">{{ error }}</p>
    <p v-else-if="hint" class="text-xs text-slate-500 m-0">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

export interface SelectOption {
  value: string
  label: string
}

interface Props {
  label?: string
  modelValue?: string
  options: SelectOption[]
  placeholder?: string
  error?: string
  hint?: string
  disabled?: boolean
  required?: boolean
  id?: string
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  required: false,
  placeholder: '— Select —',
})

defineEmits<{
  'update:modelValue': [value: string]
}>()

const selectId = computed(() => props.id || `select-${Math.random().toString(36).slice(2)}`)
</script>
