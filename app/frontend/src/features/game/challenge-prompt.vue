<script setup lang="ts">
/**
 * Free-text challenge judged by the LLM. The countdown mirrors the workflow's durable timer; when it runs out,
 * the workflow (not the browser) rules on the silence, so closing the tab does not dodge the consequence.
 */
import { computed, ref } from 'vue'
import type { ChallengePromptView } from '@/api/types'
import CountdownBadge from '@/components/ui/countdown-badge.vue'
import RetroButton from '@/components/ui/retro-button.vue'
import TextAreaField from '@/components/ui/text-area-field.vue'
import { useCountdown } from '@/composables/useCountdown'
import { statInfo } from '@/lib/game-labels'

interface Props {
  challenge: ChallengePromptView
  busy: boolean
  serverOffsetMs: number
}

const { challenge, busy, serverOffsetMs } = defineProps<Props>()
const emit = defineEmits<{ submit: [text: string] }>()

const response = ref('')
const { secondsLeft, expired, urgency } = useCountdown(() => challenge.deadline, () => serverOffsetMs)
const canSubmit = computed(() => response.value.trim().length > 0 && !expired.value && !busy)
const kicker = computed(() =>
  challenge.kind === challenge.stat ? `${challenge.kind} challenge` : `${challenge.kind} · tests ${statInfo[challenge.stat].label}`,
)

function submit() {
  if (canSubmit.value) emit('submit', response.value.trim())
}
</script>

<template>
  <form class="space-y-4" aria-labelledby="challenge-title" @submit.prevent="submit">
    <header class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="font-display text-lg tracking-widest text-muted uppercase">{{ kicker }}</p>
        <h3 id="challenge-title" class="glow font-display text-4xl">{{ challenge.title }}</h3>
      </div>
      <CountdownBadge v-if="secondsLeft !== null" :seconds-left="secondsLeft" :urgency="urgency" />
    </header>
    <p class="text-lg leading-relaxed text-dust">{{ challenge.description }}</p>
    <blockquote v-if="challenge.npcName && challenge.npcLine" class="border-l-4 border-lavender pl-3 text-lavender italic">
      <span class="not-italic">{{ challenge.npcName }}:</span> “{{ challenge.npcLine }}”
    </blockquote>
    <p class="text-sm text-muted">{{ challenge.stakes }}</p>
    <TextAreaField
      v-model="response"
      label="What do you say or do?"
      :maxlength="challenge.maxResponseLength"
      :rows="4"
      placeholder="Speak in character. Specifics persuade; vague promises don't."
      :disabled="busy || expired"
    />
    <p v-if="expired" role="status" class="text-rust">Time's up. The moment has passed...</p>
    <RetroButton type="submit" :disabled="!canSubmit" :busy="busy">Make your move</RetroButton>
  </form>
</template>
