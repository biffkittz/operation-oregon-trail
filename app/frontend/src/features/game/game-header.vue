<script setup lang="ts">
/** Voyage title bar with live-connection status. */
import type { GameView } from '@/api/types'
import type { HubStatus } from '@/composables/useGameHub'

interface Props {
  view: GameView
  hubStatus: HubStatus
  archived: boolean
}

defineProps<Props>()

const statusLabel: Record<HubStatus, string> = {
  connecting: 'Raising the station',
  live: 'Live',
  reconnecting: 'Signal lost, reconnecting',
  offline: 'Offline',
}
const statusClass: Record<HubStatus, string> = {
  connecting: 'text-phosphor',
  live: 'text-sage',
  reconnecting: 'text-phosphor',
  offline: 'text-rust',
}
</script>

<template>
  <header class="mb-4 flex flex-wrap items-end justify-between gap-3">
    <div>
      <p class="font-display text-lg tracking-[0.3em] text-muted uppercase">Berth {{ view.slotNumber }} · {{ view.tone }} · {{ view.difficulty }}</p>
      <h1 class="glow font-display text-5xl leading-none">{{ view.title }}</h1>
      <p class="text-dust">Captain {{ view.captainName }} of the {{ view.shipName }}</p>
    </div>
    <div class="text-right">
      <p class="font-display text-lg" :class="statusClass[hubStatus]" role="status">
        <span aria-hidden="true">●</span> {{ statusLabel[hubStatus] }}
      </p>
      <p v-if="archived" class="text-sm text-phosphor">Showing the archived log (the game engine is out of reach).</p>
    </div>
  </header>
</template>
