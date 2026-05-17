/**
 * Generic async data-fetching composable.
 *
 * Usage:
 *   const { data, loading, error, execute } = useApi(() => bookingService.getMy())
 *   onMounted(execute)
 */
import { ref, type Ref } from 'vue'

interface UseApiReturn<T> {
  data: Ref<T | null>
  loading: Ref<boolean>
  error: Ref<string>
  execute: () => Promise<void>
}

export function useApi<T>(fn: () => Promise<T>): UseApiReturn<T> {
  const data = ref<T | null>(null) as Ref<T | null>
  const loading = ref(false)
  const error = ref('')

  async function execute(): Promise<void> {
    loading.value = true
    error.value = ''
    try {
      data.value = await fn()
    } catch (err: unknown) {
      error.value = err instanceof Error ? err.message : 'An error occurred'
      data.value = null
    } finally {
      loading.value = false
    }
  }

  return { data, loading, error, execute }
}
