<script setup lang="ts">
/** Site settings: defaults for new voyages and display preferences. */
import { onMounted, reactive, watch } from 'vue'
import type { Settings } from '@/api/types'
import OptionCards from '@/components/ui/option-cards.vue'
import RetroButton from '@/components/ui/retro-button.vue'
import ToggleSwitch from '@/components/ui/toggle-switch.vue'
import { useAsyncAction } from '@/composables/useAsyncAction'
import { difficultyOptions, textSpeedOptions, toneOptions } from '@/lib/game-labels'
import { useNotificationsStore } from '@/stores/notifications'
import { useSettingsStore } from '@/stores/settings'

const store = useSettingsStore()
const notifications = useNotificationsStore()
const draft = reactive<Settings>({ ...store.settings })

onMounted(() => void store.load().catch(() => undefined))
watch(
  () => store.settings,
  (latest) => Object.assign(draft, latest),
)

const save = useAsyncAction(async () => {
  await store.save({ ...draft })
  notifications.push('success', 'Settings stowed.')
})
</script>

<template>
  <section class="mx-auto max-w-3xl" aria-labelledby="settings-title">
    <h1 id="settings-title" class="glow font-display text-5xl">Settings</h1>
    <p class="text-dust">Defaults apply to new voyages only. A voyage in progress keeps the tone and difficulty it began with.</p>

    <form class="mt-6 space-y-6" @submit.prevent="save.run()">
      <div class="panel space-y-5">
        <h2 class="panel-title">New voyage defaults</h2>
        <OptionCards v-model="draft.defaultTone" legend="Tone" :options="toneOptions" :columns="4" />
        <OptionCards v-model="draft.defaultDifficulty" legend="Difficulty" :options="difficultyOptions" :columns="3" />
      </div>

      <div class="panel space-y-5">
        <h2 class="panel-title">Display</h2>
        <ToggleSwitch v-model="draft.crtEffects" label="CRT effects" description="Scanlines and phosphor glow. Flicker is always off when your system asks for reduced motion." />
        <OptionCards v-model="draft.textSpeed" legend="Text speed" :options="textSpeedOptions" :columns="4" />
        <p v-if="store.reducedMotion" class="text-sm text-phosphor">Your system requests reduced motion, so text always appears instantly.</p>
      </div>

      <p v-if="save.error.value" role="alert" class="panel border-rust text-rust">{{ save.error.value.detail }}</p>
      <div class="flex justify-end">
        <RetroButton type="submit" :busy="save.pending.value">Save settings</RetroButton>
      </div>
    </form>
  </section>
</template>
