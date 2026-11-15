import { http } from './http'
import type { Settings } from './types'

export const settingsApi = {
  async get(): Promise<Settings> {
    const { data } = await http.get<Settings>('/api/settings')
    return data
  },
  async save(settings: Settings): Promise<Settings> {
    const { data } = await http.put<Settings>('/api/settings', settings)
    return data
  },
}
