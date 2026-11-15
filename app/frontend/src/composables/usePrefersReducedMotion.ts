import { getCurrentScope, onScopeDispose, readonly, ref, type Ref } from 'vue'

/** Tracks the OS "reduce motion" preference. Safe in components and Pinia setup stores (both have a scope). */
export function usePrefersReducedMotion(): Readonly<Ref<boolean>> {
  const query = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null
  const prefersReduced = ref(query?.matches ?? false)
  const listener = (event: MediaQueryListEvent) => {
    prefersReduced.value = event.matches
  }
  query?.addEventListener('change', listener)
  if (getCurrentScope()) {
    onScopeDispose(() => query?.removeEventListener('change', listener))
  }
  return readonly(prefersReduced)
}
