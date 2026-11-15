<script setup lang="ts">
/** A story beat or outcome; the player acknowledges it to move on. */
import { computed, onMounted, useTemplateRef } from 'vue'
import type { NarrativePromptView } from '@/api/types'
import RetroButton from '@/components/ui/retro-button.vue'
import StatusBadge from '@/components/ui/status-badge.vue'
import { useTypewriter } from '@/composables/useTypewriter'
import { signed } from '@/lib/format'
import { verdictInfo } from '@/lib/game-labels'

interface Props {
  narrative: NarrativePromptView
  busy: boolean
  msPerChar: number
}

const { narrative, busy, msPerChar } = defineProps<Props>()
const emit = defineEmits<{ continue: [] }>()

const { shown, done, finish } = useTypewriter(() => narrative.text, () => msPerChar)
const actions = useTemplateRef<HTMLDivElement>('actions')

// "Days" passing is neutral; every other negative change is bad news.
const changes = computed(() =>
  narrative.changes.map((c) => ({ ...c, good: c.label === 'Days' ? null : c.delta > 0 })),
)

function proceed() {
  if (!done.value) finish()
  else emit('continue')
}

onMounted(() => actions.value?.querySelector('button')?.focus())
</script>

<template>
  <article class="space-y-4" aria-labelledby="narrative-headline">
    <header class="flex flex-wrap items-center gap-3">
      <h3 id="narrative-headline" class="glow font-display text-4xl">{{ narrative.headline }}</h3>
      <StatusBadge v-if="narrative.verdict" :label="verdictInfo[narrative.verdict].label" :tone="verdictInfo[narrative.verdict].tone" />
    </header>
    <p class="sr-only">{{ narrative.text }}</p>
    <!-- Clicking the text skips the typewriter. -->
    <p aria-hidden="true" class="cursor-pointer text-lg leading-relaxed whitespace-pre-line text-dust" @click="finish">
      {{ shown }}<span v-if="!done" class="animate-blink">▮</span>
    </p>
    <ul v-if="changes.length" class="flex flex-wrap gap-2" aria-label="Consequences">
      <li
        v-for="change in changes"
        :key="change.label"
        class="rounded-sm border px-2 py-0.5 font-display text-lg"
        :class="change.good === null ? 'border-edge text-muted' : change.good ? 'border-sage/70 text-sage' : 'border-rust/70 text-rust'"
      >
        {{ change.label }} {{ signed(change.delta) }}
      </li>
    </ul>
    <div ref="actions">
      <RetroButton :busy="busy" @click="proceed">{{ done ? narrative.continueLabel : 'Skip ▸▸' }}</RetroButton>
    </div>
  </article>
</template>
