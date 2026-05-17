/**
 * Simple reactive toast notification system.
 * Toasts are rendered globally in App.vue.
 *
 * Usage:
 *   const { addToast } = useToast()
 *   addToast({ type: 'success', message: 'Saved!' })
 */
import { reactive } from 'vue'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface Toast {
  id: string
  type: ToastType
  message: string
  duration?: number
}

// Module-level singleton (shared across all composable calls)
const toasts = reactive<Toast[]>([])

export function useToast() {
  function addToast(options: Omit<Toast, 'id'> & { duration?: number }) {
    const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2)}`
    const toast: Toast = { id, type: options.type, message: options.message, duration: options.duration ?? 4000 }
    toasts.push(toast)
    if (toast.duration && toast.duration > 0) {
      setTimeout(() => removeToast(id), toast.duration)
    }
  }

  function removeToast(id: string) {
    const idx = toasts.findIndex((t) => t.id === id)
    if (idx !== -1) toasts.splice(idx, 1)
  }

  function clearAll() {
    toasts.splice(0, toasts.length)
  }

  return { toasts, addToast, removeToast, clearAll }
}
