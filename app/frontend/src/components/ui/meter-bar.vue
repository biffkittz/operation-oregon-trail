<script setup lang="ts">
/** Labeled gauge for hull, fuel, rations, morale and XP. Uses role="meter" so values are announced. */
import { computed } from 'vue'

type Tone = 'signal' | 'phosphor' | 'rust' | 'sage' | 'lavender'

interface Props {
  label: string
  value: number
  max?: number
  tone?: Tone
  /** Switch to the rust tone at or below this value (e.g. low hull). */
  lowAt?: number
  suffix?: string
  /** Overrides the visible value text (the accessible value text still uses value/max). */
  display?: string
}

const { label, value, max = 100, tone = 'signal', lowAt, suffix = '', display } = defineProps<Props>()

const percent = computed(() => (max <= 0 ? 0 : Math.min(100, Math.max(0, (value / max) * 100))))
const effectiveTone = computed<Tone>(() => (lowAt !== undefined && value <= lowAt ? 'rust' : tone))
const fillClass: Record<Tone, string> = {
  signal: 'bg-signal',
  phosphor: 'bg-phosphor',
  rust: 'bg-rust',
  sage: 'bg-sage',
  lavender: 'bg-lavender',
}
</script>

<template>
  <div
    role="meter"
    :aria-label="label"
    :aria-valuenow="value"
    aria-valuemin="0"
    :aria-valuemax="max"
    :aria-valuetext="`${value}${suffix} of ${max}${suffix}`"
  >
    <div class="flex items-baseline justify-between font-display text-lg leading-none">
      <span class="tracking-wider uppercase" :class="effectiveTone === 'rust' ? 'text-rust' : 'text-muted'">{{ label }}</span>
      <span class="text-dust tabular-nums">{{ display ?? `${value}${suffix}` }}</span>
    </div>
    <div class="mt-1 h-2 overflow-hidden rounded-sm bg-void ring-1 ring-edge">
      <!-- Width is set through a style binding, which Vue applies via the CSSOM (allowed by the strict CSP). -->
      <div class="h-full transition-[width] duration-500" :class="fillClass[effectiveTone]" :style="{ width: `${percent}%` }" />
    </div>
  </div>
</template>
