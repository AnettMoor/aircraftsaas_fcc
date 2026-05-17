<template>
  <div class="flex flex-col items-center justify-center py-12 px-6 text-center gap-2 bg-slate-50 border border-dashed border-red-100 rounded-xl" role="alert">
    <span class="text-4xl text-red-500 mb-2" aria-hidden="true">⚠</span>
    <h3 class="text-base font-semibold text-slate-700 m-0">{{ title }}</h3>
    <p v-if="message" class="text-sm text-slate-500 m-0 max-w-md">{{ message }}</p>
    <div v-if="retryable" class="mt-4">
      <AppButton variant="secondary" size="sm" @click="$emit('retry')">
        Try again
      </AppButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import AppButton from '@/components/common/AppButton.vue'

interface Props {
  title?: string
  message?: string
  retryable?: boolean
}

withDefaults(defineProps<Props>(), {
  title: 'Something went wrong',
  retryable: false,
})

defineEmits<{ retry: [] }>()
</script>
