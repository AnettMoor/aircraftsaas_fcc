<template>
  <table class="w-full text-left text-sm">
    <thead>
      <tr class="border-b border-slate-200 bg-slate-50">
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Registration</th>
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Aircraft</th>
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Year</th>
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Category</th>
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">License</th>
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Rate</th>
        <th class="px-4 py-3 text-xs font-semibold text-slate-600 uppercase tracking-wider">Status</th>
        <th class="px-4 py-3"></th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="ac in aircraft" :key="ac.id" class="border-b border-slate-100 hover:bg-slate-50 transition-colors">
        <td class="px-4 py-3 font-mono text-slate-700">{{ ac.registrationNumber }}</td>
        <td class="px-4 py-3 text-slate-700">{{ ac.make }} {{ ac.model }}</td>
        <td class="px-4 py-3 text-slate-700">{{ ac.year }}</td>
        <td class="px-4 py-3 text-slate-700">{{ ac.category }}</td>
        <td class="px-4 py-3 text-slate-700">{{ ac.requiredLicenseType || '—' }}</td>
        <td class="px-4 py-3 text-slate-700">{{ ac.hourlyRate ? `€${ac.hourlyRate.toFixed(2)}` : '—' }}</td>
        <td class="px-4 py-3">
          <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold', statusBadgeClass(ac)]">
            {{ statusLabel(ac) }}
          </span>
        </td>
        <td class="px-4 py-3">
          <div class="flex gap-3">
            <RouterLink :to="{ name: 'admin-aircraft-detail', params: { id: ac.id } }" class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors">Manage</RouterLink>
            <button class="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors" @click="$emit('edit', ac)">Edit</button>
            <button class="text-sm font-medium text-red-600 hover:text-red-700 transition-colors" @click="$emit('deactivate', ac)">Deactivate</button>
          </div>
        </td>
      </tr>
    </tbody>
  </table>
</template>

<script setup lang="ts">
import type { AircraftDto } from '@/types'

defineProps<{
  aircraft: AircraftDto[]
}>()

defineEmits<{
  edit: [ac: AircraftDto]
  deactivate: [ac: AircraftDto]
}>()

function statusLabel(ac: AircraftDto): string {
  switch (ac.status) {
    case 'InsuranceInactive': return 'Insurance Inactive'
    case 'Maintenance': return 'Maintenance'
    case 'Unavailable': return 'Unavailable'
    case 'Available': return 'Available'
    default: return ac.isAvailable ? 'Available' : 'Unavailable'
  }
}

function statusBadgeClass(ac: AircraftDto): string {
  switch (ac.status) {
    case 'InsuranceInactive': return 'bg-amber-100 text-amber-800'
    case 'Maintenance': return 'bg-blue-100 text-blue-800'
    case 'Unavailable': return 'bg-red-100 text-red-800'
    case 'Available': return 'bg-emerald-100 text-emerald-800'
    default: return ac.isAvailable ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800'
  }
}
</script>
