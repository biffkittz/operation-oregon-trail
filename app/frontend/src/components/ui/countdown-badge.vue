<script setup lang="ts">
/**
 * Visual countdown. The visible clock updates every frame, but screen readers only hear milestones
 * (60, 30 and 10 seconds) instead of a flood of announcements.
 */
import { computed, ref, watch } from 'vue'
import type { Urgency } from '@/composables/useCountdown'
import { clock } from '@/lib/format'

interface Props {
  secondsLeft: number
  urgency: Urgency
  label?: string
}

const { secondsLeft, urgency, label = 'Time left' } = defineProps<Props>()

const announcement = ref('')
const milestones = [60, 30, 10]
watch(
  () => Math.ceil(secondsLeft),
  (seconds, previous) => {
    const crossed = milestones.find((m) => previous !== undefined && previous > m && seconds <= m)
    if (crossed !== undefined) announcement.value = `${crossed} seconds left`
    if (seconds === 0) announcement.value = 'Time is up'
  },
)

const toneClass = computed(() =>
  urgency === 'critical' ? 'border-rust text-rust animate-pulse' : urgency === 'warning' ? 'border-phosphor text-phosphor' : 'border-signal-dim text-signal',
)
</script>

<template>
  <div class="inline-flex items-center gap-2 rounded-sm border-2 px-3 py-1 font-display text-2xl tabular-nums" :class="toneClass">
    <span class="text-base tracking-widest uppercase">{{ label }}</span>
    <span>{{ clock(secondsLeft) }}</span>
    <span class="sr-only" aria-live="assertive">{{ announcement }}</span>
  </div>
</template>
