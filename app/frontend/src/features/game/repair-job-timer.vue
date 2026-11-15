<script setup lang="ts">
/** Countdown for one repair job; the workflow timer completes it even if the player closes the tab. */
import type { RepairJobView } from '@/api/types'
import { useCountdown } from '@/composables/useCountdown'
import { clock } from '@/lib/format'

interface Props {
  job: RepairJobView
  serverOffsetMs: number
}

const { job, serverOffsetMs } = defineProps<Props>()
const { secondsLeft } = useCountdown(() => job.readyAt, () => serverOffsetMs)
</script>

<template>
  <li class="flex items-center justify-between gap-2 rounded-sm border border-signal-dim/60 px-2 py-1">
    <span class="text-sm text-dust">{{ job.label }}</span>
    <span class="font-display text-xl text-signal tabular-nums" :aria-label="`Ready in ${Math.ceil(secondsLeft ?? 0)} seconds`">
      {{ secondsLeft !== null && secondsLeft > 0 ? clock(secondsLeft) : 'any moment' }}
    </span>
  </li>
</template>
