import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { settingsApi } from '@/api/settings-api'
import type { Settings } from '@/api/types'
import { usePrefersReducedMotion } from '@/composables/usePrefersReducedMotion'
import { textSpeedOptions } from '@/lib/game-labels'

const defaults: Settings = { defaultTone: 'Gritty', defaultDifficulty: 'Normal', crtEffects: true, textSpeed: 'Normal' }

/** Site settings plus the derived display preferences the UI actually uses. */
export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<Settings>({ ...defaults })
  const loaded = ref(false)
  const reducedMotion = usePrefersReducedMotion()

  const crtEnabled = computed(() => settings.value.crtEffects)

  /** Typewriter speed; the OS reduced-motion preference always wins over the saved setting. */
  const msPerChar = computed(() =>
    reducedMotion.value ? 0 : (textSpeedOptions.find((o) => o.value === settings.value.textSpeed)?.msPerChar ?? 16),
  )

  async function load(): Promise<void> {
    settings.value = await settingsApi.get()
    loaded.value = true
  }

  async function save(next: Settings): Promise<void> {
    settings.value = await settingsApi.save(next)
  }

  return { settings, loaded, reducedMotion, crtEnabled, msPerChar, load, save }
})
