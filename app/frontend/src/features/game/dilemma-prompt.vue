<script setup lang="ts">
/** Three-way hazard choice; each option is an engine-defined archetype dressed up by the LLM. */
import type { DilemmaPromptView } from '@/api/types'
import CountdownBadge from '@/components/ui/countdown-badge.vue'
import { useCountdown } from '@/composables/useCountdown'

interface Props {
  dilemma: DilemmaPromptView
  busy: boolean
  serverOffsetMs: number
}

const { dilemma, busy, serverOffsetMs } = defineProps<Props>()
const emit = defineEmits<{ choose: [optionId: string] }>()

const { secondsLeft, expired, urgency } = useCountdown(() => dilemma.deadline, () => serverOffsetMs)
</script>

<template>
  <section class="space-y-4" aria-labelledby="dilemma-title">
    <header class="flex flex-wrap items-start justify-between gap-3">
      <h3 id="dilemma-title" class="glow font-display text-4xl">{{ dilemma.title }}</h3>
      <CountdownBadge v-if="secondsLeft !== null" :seconds-left="secondsLeft" :urgency="urgency" label="Decide" />
    </header>
    <p class="text-lg leading-relaxed text-dust">{{ dilemma.description }}</p>
    <ul class="space-y-2">
      <li v-for="option in dilemma.options" :key="option.id">
        <button
          type="button"
          class="flex w-full items-start gap-3 rounded-sm border-2 border-edge px-4 py-3 text-left transition-colors hover:border-phosphor hover:bg-phosphor/5 disabled:opacity-50"
          :disabled="busy || expired"
          @click="emit('choose', option.id)"
        >
          <span class="font-display text-3xl leading-none text-phosphor" aria-hidden="true">{{ option.id }}</span>
          <span>
            <span class="block font-display text-2xl leading-tight text-dust">{{ option.label }}</span>
            <span class="block text-sm text-muted">{{ option.hint }}</span>
          </span>
        </button>
      </li>
    </ul>
    <p v-if="busy" role="status" class="font-display text-xl text-phosphor">The crew braces...</p>
  </section>
</template>
