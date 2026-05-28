import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  // Microservice endpoints (override via env vars if needed)
  const USERS_URL = env.VITE_USERS_URL || 'http://localhost:5001'
  const FLEET_URL = env.VITE_FLEET_URL || 'http://localhost:5002'
  const BOOKING_URL = env.VITE_BOOKING_URL || 'http://localhost:5003'

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      proxy: {
        // ── Users microservice ─────────────────────────────────────────
        '/api/v1/identity':  { target: USERS_URL,   changeOrigin: true, secure: false },
        '/api/v1/companies': { target: USERS_URL,   changeOrigin: true, secure: false },
        '/api/v1/licenses':  { target: USERS_URL,   changeOrigin: true, secure: false },
        '/api/v1/admin':     { target: USERS_URL,   changeOrigin: true, secure: false },

        // ── Fleet microservice ─────────────────────────────────────────
        '/api/v1/aircraft':    { target: FLEET_URL, changeOrigin: true, secure: false },
        '/api/v1/airports':    { target: FLEET_URL, changeOrigin: true, secure: false },
        '/api/v1/maintenance': { target: FLEET_URL, changeOrigin: true, secure: false },

        // ── Booking microservice ───────────────────────────────────────
        '/api/v1/bookings': { target: BOOKING_URL, changeOrigin: true, secure: false },
        '/api/v1/reviews':  { target: BOOKING_URL, changeOrigin: true, secure: false },
      },
    },
    build: {
      outDir: 'dist',
      sourcemap: mode !== 'production',
      rollupOptions: {
        output: {
          manualChunks: {
            'vendor-vue': ['vue', 'vue-router', 'pinia'],
            'vendor-http': ['axios'],
          },
        },
      },
    },
  }
})
