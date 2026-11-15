<script setup lang="ts">
/** Small colored label for verdicts and voyage status. */
interface Props {
  label: string
  tone: 'good' | 'mixed' | 'bad'
}

defineProps<Props>()
const toneClass = { good: 'border-sage text-sage', mixed: 'border-phosphor text-phosphor', bad: 'border-rust text-rust' } as const
</script>

<template>
  <span class="inline-block rounded-sm border px-2 py-0.5 font-display text-lg leading-none tracking-wider uppercase" :class="toneClass[tone]">
    {{ label }}
  </span>
</template>
