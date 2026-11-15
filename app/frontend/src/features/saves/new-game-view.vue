<script setup lang="ts">
/**
 * New-voyage wizard. Tone and difficulty default to the saved settings but are snapshotted into the game:
 * changing settings later never alters this voyage.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { GameLimits, type Difficulty, type GameTone } from '@/api/types'
import OptionCards from '@/components/ui/option-cards.vue'
import RetroButton from '@/components/ui/retro-button.vue'
import TextAreaField from '@/components/ui/text-area-field.vue'
import TextField from '@/components/ui/text-field.vue'
import { useAsyncAction } from '@/composables/useAsyncAction'
import { difficultyOptions, toneOptions } from '@/lib/game-labels'
import { useSavesStore } from '@/stores/saves'
import { useSettingsStore } from '@/stores/settings'

const route = useRoute()
const router = useRouter()
const saves = useSavesStore()
const settings = useSettingsStore()

const slotNumber = computed(() => {
  const value = Number(route.query.slot)
  return Number.isInteger(value) && value >= 1 && value <= GameLimits.maxSaveSlots ? value : null
})

const form = reactive({
  captainName: '',
  shipName: '',
  title: '',
  tone: settings.settings.defaultTone as GameTone,
  difficulty: settings.settings.defaultDifficulty as Difficulty,
  storySeed: '',
})
const hint = ref('')
const touched = reactive<Record<string, boolean>>({})
const submitted = ref(false)

onMounted(async () => {
  if (!settings.loaded) {
    await settings.load().catch(() => undefined)
    form.tone = settings.settings.defaultTone
    form.difficulty = settings.settings.defaultDifficulty
  }
})

const clientErrors = computed<Record<string, string | null>>(() => ({
  captainName: form.captainName.trim() ? null : 'Every wagon needs a captain.',
  shipName: form.shipName.trim() ? null : 'Name your wagon.',
  storySeed: form.storySeed.trim().length >= 10 ? null : 'Give the voyage a premise (at least 10 characters), or generate one.',
}))

const create = useAsyncAction(() =>
  saves.create({
    slotNumber: slotNumber.value,
    captainName: form.captainName.trim(),
    shipName: form.shipName.trim(),
    title: form.title.trim() || null,
    tone: form.tone,
    difficulty: form.difficulty,
    storySeed: form.storySeed.trim(),
  }),
)

const generate = useAsyncAction(() => saves.generateSeed(form.tone, hint.value.trim() || null))

/** Shows an error once the field was touched or the form submitted; server errors win. */
function errorFor(field: keyof typeof clientErrors.value): string | null {
  const server = create.error.value?.fieldErrors[field]?.[0]
  if (server) return server
  return touched[field] || submitted.value ? (clientErrors.value[field] ?? null) : null
}

async function generateSeed() {
  const result = await generate.run()
  if (result) {
    form.storySeed = result.seed
    if (!form.title.trim()) form.title = result.title
  }
}

async function submit() {
  submitted.value = true
  if (Object.values(clientErrors.value).some((e) => e)) return
  const created = await create.run()
  if (created) await router.push({ name: 'game', params: { gameId: created.gameId } })
}
</script>

<template>
  <section class="mx-auto max-w-3xl" aria-labelledby="new-title">
    <h1 id="new-title" class="glow font-display text-5xl">Outfit a new wagon</h1>
    <p class="text-dust">
      <template v-if="slotNumber">Berth {{ slotNumber }} is yours.</template>
      Fill the manifest, pick your poison, and cast off from Independence Ring.
    </p>

    <form class="mt-6 space-y-6" novalidate @submit.prevent="submit">
      <div class="panel grid gap-4 sm:grid-cols-2">
        <TextField
          v-model="form.captainName"
          label="Captain"
          :maxlength="GameLimits.maxNameLength"
          autocomplete="off"
          :error="errorFor('captainName')"
          required
          @blur="touched.captainName = true"
        />
        <TextField
          v-model="form.shipName"
          label="Wagon name"
          :maxlength="GameLimits.maxNameLength"
          autocomplete="off"
          :error="errorFor('shipName')"
          required
          @blur="touched.shipName = true"
        />
        <TextField v-model="form.title" class="sm:col-span-2" label="Voyage title" :maxlength="80" hint="Optional. Leave blank to name it after your wagon." />
      </div>

      <div class="panel space-y-5">
        <OptionCards v-model="form.tone" legend="Tone" :options="toneOptions" :columns="4" />
        <OptionCards v-model="form.difficulty" legend="Difficulty" :options="difficultyOptions" :columns="3" />
      </div>

      <div class="panel space-y-4">
        <TextAreaField
          v-model="form.storySeed"
          label="Story seed"
          :maxlength="GameLimits.maxStorySeedLength"
          :rows="5"
          placeholder="Why are you hauling across the Reach, and what can't you afford to lose?"
          :error="errorFor('storySeed')"
          @blur="touched.storySeed = true"
        />
        <div class="flex flex-wrap items-end gap-3">
          <div class="min-w-48 flex-1">
            <TextField v-model="hint" label="Inspire the AI (optional)" :maxlength="200" hint="e.g. 'a traveling circus'" />
          </div>
          <RetroButton variant="secondary" :busy="generate.pending.value" @click="generateSeed">Generate with AI</RetroButton>
        </div>
        <p v-if="generate.error.value" role="alert" class="field-error">{{ generate.error.value.detail }}</p>
      </div>

      <p v-if="create.error.value && !Object.keys(create.error.value.fieldErrors).length" role="alert" class="panel border-rust text-rust">
        {{ create.error.value.detail }}
      </p>
      <div class="flex justify-end gap-3">
        <RetroButton variant="ghost" @click="router.push({ name: 'hangar' })">Cancel</RetroButton>
        <RetroButton type="submit" :busy="create.pending.value">Cast off</RetroButton>
      </div>
    </form>
  </section>
</template>
