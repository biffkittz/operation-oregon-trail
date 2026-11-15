import { http } from './http'

export const authApi = {
  async login(password: string): Promise<void> {
    await http.post('/api/auth/login', { password })
  },
  async logout(): Promise<void> {
    await http.post('/api/auth/logout')
  },
  async session(): Promise<boolean> {
    const { data } = await http.get<{ authenticated: boolean }>('/api/auth/session')
    return data.authenticated
  },
}
