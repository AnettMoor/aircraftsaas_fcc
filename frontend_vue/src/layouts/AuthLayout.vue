<template>
  <div class="auth-layout min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-blue-700 p-4 relative overflow-hidden">
    <div class="relative bg-white rounded-2xl shadow-[0_8px_32px_rgba(0,0,0,0.2),0_2px_8px_rgba(0,0,0,0.1)] p-10 w-full max-w-[440px]">
      <div class="flex items-center gap-2 mb-8 justify-center">
        <span class="text-3xl">✈</span>
        <span class="text-2xl font-bold text-slate-900 tracking-tight">AircraftSaaS</span>
      </div>
      <RouterView />
    </div>
  </div>
</template>

<style scoped>
/* Subtle animated gradient mesh — kept as custom CSS (complex pseudo-element) */
.auth-layout::before {
  content: '';
  position: absolute;
  inset: 0;
  background:
    radial-gradient(ellipse at 20% 50%, rgba(29, 78, 216, 0.18) 0%, transparent 50%),
    radial-gradient(ellipse at 80% 20%, rgba(29, 78, 216, 0.12) 0%, transparent 50%),
    radial-gradient(ellipse at 60% 80%, rgba(30, 64, 175, 0.1) 0%, transparent 50%);
  animation: auth-gradient-shift 12s ease-in-out infinite alternate;
}

@keyframes auth-gradient-shift {
  0% { opacity: 0.7; transform: scale(1); }
  100% { opacity: 1; transform: scale(1.05); }
}
</style>
