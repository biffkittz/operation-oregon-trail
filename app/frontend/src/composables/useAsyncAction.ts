import { ref, type Ref } from 'vue'
import { toApiError, type ApiError } from '@/api/http'

export interface AsyncAction<TArgs extends unknown[], TResult> {
  run: (...args: TArgs) => Promise<TResult | undefined>
  pending: Ref<boolean>
  error: Ref<ApiError | null>
}

/** Wraps an async operation with explicit loading and error state for forms and buttons. */
export function useAsyncAction<TArgs extends unknown[], TResult>(
  action: (...args: TArgs) => Promise<TResult>,
): AsyncAction<TArgs, TResult> {
  const pending = ref(false)
  const error = ref<ApiError | null>(null) as Ref<ApiError | null>

  async function run(...args: TArgs): Promise<TResult | undefined> {
    pending.value = true
    error.value = null
    try {
      return await action(...args)
    } catch (caught) {
      error.value = toApiError(caught)
      return undefined
    } finally {
      pending.value = false
    }
  }

  return { run, pending, error }
}
