<template>
  <div class="flex flex-col gap-1">
    <label v-if="label" :for="inputId" class="text-xs font-medium text-slate-700">
      {{ label }}
      <span v-if="required" class="text-red-500 ml-0.5">*</span>
    </label>
    <input
      :id="inputId"
      :class="[
        'w-full px-3 py-2 border rounded-lg text-sm text-slate-900 bg-white outline-none transition-all duration-150 min-h-[2.5rem]',
        'placeholder:text-slate-400',
        'focus:border-blue-500 focus:ring-2 focus:ring-blue-500/15',
        'disabled:bg-slate-50 disabled:text-slate-500 disabled:cursor-not-allowed',
        error ? 'border-red-500 focus:ring-red-500/15' : 'border-slate-300',
      ]"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      :autocomplete="autocomplete"
      v-bind="$attrs"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      @blur="$emit('blur', $event)"
    />
    <p v-if="error" class="text-xs text-red-500 m-0" role="alert">{{ error }}</p>
    <p v-else-if="hint" class="text-xs text-slate-500 m-0">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  label?: string
  modelValue?: string
  type?: string
  placeholder?: string
  error?: string
  hint?: string
  disabled?: boolean
  required?: boolean
  autocomplete?: string
  id?: string
}

const props = withDefaults(defineProps<Props>(), {
  type: 'text',
  disabled: false,
  required: false,
})

defineEmits<{
  'update:modelValue': [value: string]
  blur: [event: FocusEvent]
}>()

const inputId = computed(() => props.id || `input-${Math.random().toString(36).slice(2)}`)
</script>
