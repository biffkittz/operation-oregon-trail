<script setup lang="ts">
/** How the voyage ended. */
import { RouterLink } from 'vue-router'
import type { FinaleView } from '@/api/types'

interface Props {
  finale: FinaleView
}

defineProps<Props>()
</script>

<template>
  <section class="space-y-4 text-center" aria-labelledby="finale-title">
    <p class="font-display text-xl tracking-[0.4em] uppercase" :class="finale.outcome === 'Victory' ? 'text-sage' : 'text-rust'">
      {{ finale.outcome === 'Victory' ? 'Journey complete' : 'Voyage lost' }}
    </p>
    <h3 id="finale-title" class="glow font-display text-5xl">{{ finale.outcome === 'Victory' ? 'Willamette Landing' : 'The Trail Ends Here' }}</h3>
    <p class="mx-auto max-w-2xl text-lg leading-relaxed whitespace-pre-line text-dust">{{ finale.epilogue }}</p>
    <dl class="mx-auto flex max-w-sm justify-around font-display text-2xl">
      <div><dt class="text-muted uppercase">Days</dt><dd class="text-dust">{{ finale.days }}</dd></div>
      <div><dt class="text-muted uppercase">Score</dt><dd class="text-phosphor">{{ finale.score }}</dd></div>
    </dl>
    <RouterLink :to="{ name: 'hangar' }" class="inline-block font-display text-2xl text-signal underline underline-offset-4">Back to the hangar</RouterLink>
  </section>
</template>
