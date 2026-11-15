<script setup lang="ts">
/**
 * Game screen container: the only component in this feature that talks to the store and SignalR. Children
 * receive the view through props and emit typed commands.
 */
import { onUnmounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import type { GameCommand } from '@/api/types'
import LoadingCursor from '@/components/ui/loading-cursor.vue'
import RetroButton from '@/components/ui/retro-button.vue'
import { useGameHub } from '@/composables/useGameHub'
import { useGameStore } from '@/stores/game'
import { useNotificationsStore } from '@/stores/notifications'
import { useSettingsStore } from '@/stores/settings'
import ActionPanel from './action-panel.vue'
import CrewPanel from './crew-panel.vue'
import GameHeader from './game-header.vue'
import JournalLog from './journal-log.vue'
import RouteMap from './route-map.vue'
import StatusPanel from './status-panel.vue'

const props = defineProps<{ gameId: string }>()

const game = useGameStore()
const settings = useSettingsStore()
const notifications = useNotificationsStore()
const selectedRoute = ref<string | null>(null)

watch(
  () => props.gameId,
  (id) => {
    selectedRoute.value = null
    void game.load(id)
  },
  { immediate: true },
)

const { status: hubStatus } = useGameHub(
  () => props.gameId,
  (snapshot) => {
    if (snapshot.view.gameId === props.gameId) game.applySnapshot(snapshot)
  },
  () => void game.load(props.gameId),
)

// A new phase invalidates the route selection.
watch(
  () => game.view?.phase,
  () => (selectedRoute.value = null),
)

async function send(command: GameCommand) {
  const ok = await game.send(command)
  if (!ok && game.error) notifications.push('error', game.error.detail)
}

onUnmounted(() => game.reset())
</script>

<template>
  <div>
    <LoadingCursor v-if="game.loading && !game.view" label="Hailing your wagon" />

    <div v-else-if="!game.view && game.error" role="alert" class="panel border-rust">
      <p class="text-rust">{{ game.error.status === 404 ? 'That voyage is not in the hangar.' : game.error.detail }}</p>
      <div class="mt-4 flex gap-3">
        <RetroButton variant="secondary" @click="game.load(props.gameId)">Try again</RetroButton>
        <RouterLink :to="{ name: 'hangar' }" class="self-center font-display text-xl text-signal underline">Back to the hangar</RouterLink>
      </div>
    </div>

    <template v-else-if="game.view">
      <GameHeader :view="game.view" :hub-status="hubStatus" :archived="game.source === 'archive'" />
      <div class="grid gap-4 lg:grid-cols-[17rem_minmax(0,1fr)] xl:grid-cols-[17rem_minmax(0,1fr)_24rem]">
        <div class="order-3 min-w-0 space-y-4 lg:order-1">
          <StatusPanel :resources="game.view.resources" :day="game.view.day" :repair-jobs="game.view.repairJobs" :server-offset-ms="game.serverOffsetMs" />
          <CrewPanel
            :stats="game.view.stats"
            :allies="game.view.allies"
            :upgrade-points="game.view.upgradePoints"
            :can-upgrade="!game.busy && (game.view.phase === 'ChoosingRoute' || game.view.phase === 'Narrative')"
            @upgrade="(stat) => send({ kind: 'upgrade', stat })"
          />
        </div>
        <div class="order-1 min-w-0 space-y-4 lg:order-2">
          <ActionPanel
            v-model:selected-route="selectedRoute"
            :view="game.view"
            :busy="game.busy"
            :pending-message="game.pendingMessage"
            :ms-per-char="settings.msPerChar"
            :server-offset-ms="game.serverOffsetMs"
            @command="send"
          />
          <JournalLog :entries="game.journal" :has-older="game.hasOlderJournal" :ms-per-char="settings.msPerChar" @load-older="game.loadOlderJournal()" />
        </div>
        <div class="order-2 min-w-0 lg:order-3 lg:col-span-2 xl:col-span-1">
          <RouteMap :map="game.view.map" :selected-node-id="selectedRoute" @select="(id) => (selectedRoute = id)" />
        </div>
      </div>
    </template>
  </div>
</template>
