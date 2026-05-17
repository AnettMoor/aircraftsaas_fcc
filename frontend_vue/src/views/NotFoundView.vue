<template>
  <div class="min-h-screen flex items-center justify-center bg-slate-50 p-8">
    <div class="text-center max-w-md">
      <p class="text-[6rem] font-extrabold text-slate-200 mb-2 leading-none tracking-tighter">404</p>
      <h1 class="text-2xl font-bold text-slate-900 mb-3">Page not found</h1>
      <p class="text-base text-slate-500 mb-8 leading-relaxed">The page you were looking for doesn't exist or has been moved.</p>
      <RouterLink
        :to="{ name: 'client-dashboard' }"
        class="inline-flex items-center gap-2 bg-gradient-to-b from-blue-600 to-blue-700 text-white px-6 py-2.5 rounded-xl font-semibold text-base no-underline shadow-btn hover:from-blue-700 hover:to-blue-800 hover:shadow-btn-hover hover:-translate-y-px transition-all duration-200"
      >
        ← Go to dashboard
      </RouterLink>
    </div>
  </div>
</template>

<script setup lang="ts"></script>
