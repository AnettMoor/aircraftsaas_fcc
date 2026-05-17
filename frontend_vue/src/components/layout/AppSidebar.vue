<template>
  <!-- Mobile overlay backdrop -->
  <Teleport to="body">
    <Transition name="sidebar-overlay">
      <div
        v-if="mobileOpen"
        class="fixed inset-0 z-40 bg-black/60 backdrop-blur-sm md:hidden"
        @click="closeMobile"
      />
    </Transition>
  </Teleport>

  <!-- Sidebar -->
  <aside
    :class="[
      'sidebar-shell group/sidebar flex flex-col flex-shrink-0 h-screen z-50 transition-all duration-300 ease-out sticky top-0',
      // Desktop: static sidebar; Mobile: fixed overlay
      'max-md:fixed max-md:top-0 max-md:left-0 max-md:bottom-0',
      // Mobile slide-in
      mobileOpen ? 'max-md:translate-x-0' : 'max-md:-translate-x-full',
      // Width states
      collapsed && !mobileOpen ? 'w-[72px]' : 'w-[260px]',
      'max-md:w-[280px]',
    ]"
  >
    <!-- Glassmorphism inner container -->
    <div class="flex flex-col h-full sidebar-inner relative overflow-hidden">
      <!-- Subtle gradient overlay -->
      <div class="absolute inset-0 bg-gradient-to-b from-white/[0.04] to-transparent pointer-events-none" />

      <!-- ===== Brand Header ===== -->
      <div
        :class="[
          'relative flex items-center border-b border-white/[0.06] transition-all duration-300',
          collapsed && !mobileOpen ? 'flex-col gap-2 px-2 py-4' : 'flex-row gap-3 px-5 py-5',
        ]"
      >
        <!-- Brand icon (clickable to expand when collapsed) -->
        <button
          v-if="collapsed && !mobileOpen"
          class="hidden md:flex items-center justify-center w-10 h-10 rounded-xl bg-gradient-to-br from-blue-600 to-blue-400 shadow-lg shadow-blue-600/25 flex-shrink-0 border-none cursor-pointer transition-transform duration-200 hover:scale-105"
          title="Expand sidebar"
          @click="toggleCollapsed"
        >
          <Plane class="w-5 h-5 text-white -rotate-45" :stroke-width="2.25" />
        </button>
        <!-- Brand icon (non-clickable when expanded) -->
        <div
          v-else
          class="flex items-center justify-center w-9 h-9 rounded-xl bg-gradient-to-br from-blue-600 to-blue-400 shadow-lg shadow-blue-600/25 flex-shrink-0"
        >
          <Plane class="w-5 h-5 text-white -rotate-45" :stroke-width="2.25" />
        </div>

        <Transition name="sidebar-label">
          <span
            v-if="showLabels"
            class="font-bold text-[15px] text-white tracking-tight whitespace-nowrap overflow-hidden"
          >
            AircraftSaaS
          </span>
        </Transition>

        <!-- Collapse toggle (desktop only, visible when expanded) -->
        <button
          v-if="!collapsed"
          class="hidden md:flex ml-auto items-center justify-center w-7 h-7 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.08] transition-all duration-200 flex-shrink-0"
          title="Collapse sidebar"
          @click="toggleCollapsed"
        >
          <PanelLeftClose class="w-4 h-4" />
        </button>

        <!-- Expand toggle (desktop only, visible when collapsed) -->
        <button
          v-if="collapsed && !mobileOpen"
          class="hidden md:flex items-center justify-center w-8 h-8 rounded-lg text-slate-500 hover:text-white hover:bg-white/[0.08] transition-all duration-200 flex-shrink-0"
          title="Expand sidebar"
          @click="toggleCollapsed"
        >
          <PanelLeftOpen class="w-4 h-4" />
        </button>

        <!-- Close button (mobile only) -->
        <button
          class="md:hidden ml-auto flex items-center justify-center w-7 h-7 rounded-lg text-slate-400 hover:text-white hover:bg-white/[0.08] transition-all duration-200"
          @click="closeMobile"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <!-- ===== Navigation ===== -->
      <nav class="flex-1 overflow-y-auto overflow-x-hidden py-4 px-3 space-y-6 sidebar-scrollbar">
        <div v-for="section in sections" :key="section.label">
          <!-- Section label -->
          <Transition name="sidebar-label">
            <p
              v-if="showLabels"
              class="text-[10px] font-semibold tracking-[0.1em] text-slate-500 uppercase mb-2 px-3"
            >
              {{ section.label }}
            </p>
          </Transition>

          <div class="space-y-1">
            <div v-for="item in section.items" :key="item.name" class="relative group/item">
              <RouterLink
                :to="item.to"
                :class="[
                  'sidebar-nav-item flex items-center gap-3 py-2.5 rounded-xl text-[13.5px] font-medium transition-all duration-200 no-underline relative',
                  collapsed && !mobileOpen ? 'px-0 justify-center mx-0' : 'px-3 mx-0',
                  isActive(item.to)
                    ? 'sidebar-nav-active text-white'
                    : 'text-slate-400 hover:text-white hover:bg-white/[0.06]',
                ]"
                @click="closeMobileOnNav"
              >
                <!-- Active accent bar -->
                <div
                  v-if="isActive(item.to)"
                  class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-5 rounded-r-full bg-blue-500 shadow-[0_0_8px_rgba(37,99,235,0.6)]"
                />

                <!-- Icon -->
                <span
                  :class="[
                    'flex items-center justify-center flex-shrink-0 transition-colors duration-200',
                    collapsed && !mobileOpen ? 'w-10 h-10 rounded-xl' : 'w-8 h-8 rounded-lg',
                    isActive(item.to)
                      ? 'text-blue-500'
                      : 'text-slate-500 group-hover/item:text-slate-300',
                  ]"
                >
                  <component :is="item.icon" class="w-[18px] h-[18px]" :stroke-width="1.75" />
                </span>

                <!-- Label -->
                <Transition name="sidebar-label">
                  <span v-if="showLabels" class="whitespace-nowrap overflow-hidden text-ellipsis">
                    {{ item.label }}
                  </span>
                </Transition>
              </RouterLink>

              <!-- Tooltip when collapsed (desktop) -->
              <div
                v-if="collapsed && !mobileOpen"
                class="absolute left-full top-1/2 -translate-y-1/2 ml-3 px-2.5 py-1.5 rounded-lg bg-slate-800 text-white text-xs font-medium shadow-xl border border-white/10 whitespace-nowrap opacity-0 pointer-events-none group-hover/item:opacity-100 transition-opacity duration-200 z-[100] hidden md:block"
              >
                {{ item.label }}
                <div class="absolute right-full top-1/2 -translate-y-1/2 border-[5px] border-transparent border-r-slate-800" />
              </div>
            </div>
          </div>
        </div>
      </nav>

      <!-- ===== Divider ===== -->
      <div class="mx-4 border-t border-white/[0.06]" />

      <!-- ===== Sign Out Button ===== -->
      <div class="px-3 pb-4">
        <button
          :class="[
            'sidebar-signout flex items-center gap-3 w-full rounded-xl text-[13px] font-medium transition-all duration-200 cursor-pointer border-none text-slate-500 hover:text-red-400 hover:bg-red-500/[0.08]',
            collapsed && !mobileOpen ? 'justify-center px-0 py-2.5' : 'px-3 py-2.5',
          ]"
          @click="$emit('logout')"
        >
          <LogOut class="w-[18px] h-[18px] flex-shrink-0" :stroke-width="1.75" />
          <Transition name="sidebar-label">
            <span v-if="showLabels" class="whitespace-nowrap">Sign out</span>
          </Transition>
        </button>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed, type Component } from 'vue'
import { useRoute } from 'vue-router'
import {
  Plane,
  PanelLeftClose,
  PanelLeftOpen,
  X,
  LogOut,
} from 'lucide-vue-next'

// ----------------------------------------------------------------
// Props
// ----------------------------------------------------------------
export interface NavItem {
  name: string
  label: string
  icon: Component
  to: string
}

export interface NavSection {
  label: string
  items: NavItem[]
}

const props = defineProps<{
  sections: NavSection[]
  mobileOpen: boolean
}>()

const emit = defineEmits<{
  logout: []
  'update:mobileOpen': [value: boolean]
}>()

// ----------------------------------------------------------------
// State
// ----------------------------------------------------------------
const route = useRoute()
const collapsed = ref(false)

const showLabels = computed(() => {
  // On mobile when open, always show labels
  if (props.mobileOpen) return true
  return !collapsed.value
})

// ----------------------------------------------------------------
// Methods
// ----------------------------------------------------------------
function toggleCollapsed() {
  collapsed.value = !collapsed.value
}

function closeMobile() {
  emit('update:mobileOpen', false)
}

function closeMobileOnNav() {
  // Close on mobile after clicking a nav item
  if (props.mobileOpen) {
    emit('update:mobileOpen', false)
  }
}

function isActive(to: string): boolean {
  return route.path.startsWith(to)
}
</script>

<style scoped>
/* ===== Sidebar Shell ===== */
.sidebar-shell {
  background: linear-gradient(
    180deg,
    rgb(15 23 42) 0%,       /* slate-900 */
    rgb(10 15 30) 50%,
    rgb(8 12 24) 100%
  );
  border-right: 1px solid rgba(255 255 255 / 0.06);
}

.sidebar-inner {
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
}

/* ===== Active nav item glow ===== */
.sidebar-nav-active {
  background: linear-gradient(135deg, rgba(29 78 216 / 0.15), rgba(29 78 216 / 0.06));
  box-shadow:
    0 0 24px rgba(29 78 216 / 0.12),
    inset 0 1px 0 rgba(255 255 255 / 0.05);
}

/* ===== Custom scrollbar ===== */
.sidebar-scrollbar {
  scrollbar-width: thin;
  scrollbar-color: rgba(255 255 255 / 0.08) transparent;
}

.sidebar-scrollbar::-webkit-scrollbar {
  width: 4px;
}

.sidebar-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}

.sidebar-scrollbar::-webkit-scrollbar-thumb {
  background: rgba(255 255 255 / 0.08);
  border-radius: 4px;
}

.sidebar-scrollbar::-webkit-scrollbar-thumb:hover {
  background: rgba(255 255 255 / 0.15);
}

/* ===== Transitions ===== */
.sidebar-label-enter-active {
  transition: opacity 200ms ease-out, transform 200ms ease-out;
}
.sidebar-label-leave-active {
  transition: opacity 150ms ease-in, transform 150ms ease-in;
}
.sidebar-label-enter-from {
  opacity: 0;
  transform: translateX(-4px);
}
.sidebar-label-leave-to {
  opacity: 0;
  transform: translateX(-4px);
}

.sidebar-overlay-enter-active {
  transition: opacity 250ms ease-out;
}
.sidebar-overlay-leave-active {
  transition: opacity 200ms ease-in;
}
.sidebar-overlay-enter-from,
.sidebar-overlay-leave-to {
  opacity: 0;
}
</style>
