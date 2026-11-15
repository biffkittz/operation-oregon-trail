import { defineStore } from 'pinia'
import { ref } from 'vue'

export type ToastKind = 'info' | 'success' | 'error'

export interface Toast {
  id: number
  kind: ToastKind
  message: string
}

/** Transient messages, rendered by the toast region in an ARIA live region. */
export const useNotificationsStore = defineStore('notifications', () => {
  const toasts = ref<Toast[]>([])
  let nextId = 1

  function dismiss(id: number) {
    toasts.value = toasts.value.filter((toast) => toast.id !== id)
  }

  function push(kind: ToastKind, message: string, timeoutMs = 6000): number {
    const id = nextId++
    toasts.value = [...toasts.value.slice(-3), { id, kind, message }]
    if (timeoutMs > 0) setTimeout(() => dismiss(id), timeoutMs)
    return id
  }

  return { toasts, push, dismiss }
})
