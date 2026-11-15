import { defineStore } from 'pinia'
import { ref } from 'vue'
import { authApi } from '@/api/auth-api'

/**
 * Session state. The session itself is an HttpOnly cookie the browser manages; this store only remembers
 * whether we have one. `null` means "not checked yet" (the router guard checks once on first navigation).
 */
export const useAuthStore = defineStore('auth', () => {
  const authenticated = ref<boolean | null>(null)

  async function checkSession(): Promise<boolean> {
    try {
      authenticated.value = await authApi.session()
    } catch {
      authenticated.value = false
    }
    return authenticated.value
  }

  async function login(password: string): Promise<void> {
    await authApi.login(password)
    authenticated.value = true
  }

  async function logout(): Promise<void> {
    try {
      await authApi.logout()
    } finally {
      authenticated.value = false
    }
  }

  function markSignedOut() {
    authenticated.value = false
  }

  return { authenticated, checkSession, login, logout, markSignedOut }
})
