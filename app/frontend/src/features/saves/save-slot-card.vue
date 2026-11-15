<script setup lang="ts">
/** One save slot: empty berth, voyage in progress, or finished voyage. Presentational only. */
import { computed } from 'vue'
import type { SaveSlot } from '@/api/types'
import MeterBar from '@/components/ui/meter-bar.vue'
import RetroButton from '@/components/ui/retro-button.vue'
import StatusBadge from '@/components/ui/status-badge.vue'
import { relativeTime } from '@/lib/format'
import { statusInfo } from '@/lib/game-labels'

interface Props {
  entry: SaveSlot
}

const { entry } = defineProps<Props>()
const emit = defineEmits<{ resume: [gameId: string]; scuttle: [gameId: string, title: string]; create: [slotNumber: number] }>()

const game = computed(() => entry.game)
const headingId = computed(() => `slot-${entry.slotNumber}-title`)
</script>

<template>
  <article class="panel flex flex-col gap-3" :aria-labelledby="headingId">
    <header class="flex items-start justify-between gap-2">
      <p class="font-display text-lg tracking-[0.3em] text-muted uppercase">Berth {{ entry.slotNumber.toString().padStart(2, '0') }}</p>
      <StatusBadge v-if="game" :label="statusInfo[game.status].label" :tone="statusInfo[game.status].tone" />
    </header>

    <template v-if="game">
      <div>
        <h2 :id="headingId" class="font-display text-3xl leading-none">{{ game.title }}</h2>
        <p class="mt-1 text-sm text-muted">Capt. {{ game.captainName }} · the {{ game.shipName }} · {{ game.tone }} / {{ game.difficulty }}</p>
      </div>
      <dl class="grid grid-cols-3 gap-2 font-display text-lg">
        <div><dt class="text-muted uppercase">Day</dt><dd class="text-dust">{{ game.day }}</dd></div>
        <div><dt class="text-muted uppercase">Crew</dt><dd class="text-dust">{{ game.crew }}</dd></div>
        <div v-if="game.score !== null"><dt class="text-muted uppercase">Score</dt><dd class="text-phosphor">{{ game.score }}</dd></div>
      </dl>
      <p class="text-sm text-dust">At {{ game.locationName }} · updated {{ relativeTime(game.updatedAt) }}</p>
      <MeterBar label="Hull" :value="game.hull" suffix="%" :low-at="30" />
      <div class="mt-auto flex gap-2 pt-2">
        <RetroButton class="flex-1" @click="emit('resume', game.gameId)">{{ game.status === 'Active' ? 'Resume' : 'Review' }}</RetroButton>
        <RetroButton variant="danger" :aria-label="`Scuttle ${game.title}`" @click="emit('scuttle', game.gameId, game.title)">Scuttle</RetroButton>
      </div>
    </template>

    <template v-else>
      <h2 :id="headingId" class="font-display text-3xl leading-none text-muted">Empty berth</h2>
      <p class="text-sm text-muted">A patch of scuffed deck plating, waiting for a wagon.</p>
      <RetroButton class="mt-auto" variant="secondary" @click="emit('create', entry.slotNumber)">New voyage</RetroButton>
    </template>
  </article>
</template>
