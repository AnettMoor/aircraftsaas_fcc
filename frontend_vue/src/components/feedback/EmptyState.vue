<template>
  <div class="flex flex-col items-center justify-center py-12 px-6 text-center gap-2 bg-slate-50 border border-dashed border-slate-200 rounded-xl">
    <span v-if="icon" class="text-5xl leading-none mb-2 opacity-70" aria-hidden="true">{{ icon }}</span>
    <h3 class="text-base font-semibold text-slate-700 m-0">{{ title }}</h3>
    <p v-if="description" class="text-sm text-slate-500 m-0 max-w-md">{{ description }}</p>
    <div v-if="$slots.action" class="mt-4">
      <slot name="action" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  icon?: string
  title: string
  description?: string
}

defineProps<Props>()
</script>
