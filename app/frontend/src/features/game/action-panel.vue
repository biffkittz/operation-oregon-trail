<script setup lang="ts">
/** Shows the right controls for the current phase and turns their events into typed game commands. */
import type { GameCommand, GameView } from '@/api/types'
import LoadingCursor from '@/components/ui/loading-cursor.vue'
import type { SkirmishTally } from '@/lib/skirmish'
import ChallengePrompt from './challenge-prompt.vue'
import DilemmaPrompt from './dilemma-prompt.vue'
import EmergencyFuelOffer from './emergency-fuel-offer.vue'
import FinalePanel from './finale-panel.vue'
import NarrativeCard from './narrative-card.vue'
import OutpostPanel from './outpost-panel.vue'
import RoutePicker from './route-picker.vue'
import SkirmishArena from './skirmish-arena.vue'

interface Props {
  view: GameView
  busy: boolean
  pendingMessage: string | null
  msPerChar: number
  serverOffsetMs: number
}

const { view, busy, pendingMessage, msPerChar, serverOffsetMs } = defineProps<Props>()
const selectedRoute = defineModel<string | null>('selectedRoute', { required: true })
const emit = defineEmits<{ command: [command: GameCommand] }>()

function report(tally: SkirmishTally) {
  emit('command', { kind: 'skirmish', hits: tally.hits, shotsFired: tally.shotsFired, elapsedMs: tally.elapsedMs })
}
</script>

<template>
  <section class="panel" aria-label="Your move">
    <p v-if="pendingMessage" role="status" class="mb-3 font-display text-xl text-phosphor">
      {{ pendingMessage }} <span class="animate-blink" aria-hidden="true">▮</span>
    </p>

    <LoadingCursor v-if="view.phase === 'Initializing'" label="Charting the Oregon Reach" />

    <NarrativeCard
      v-else-if="view.phase === 'Narrative' && view.prompt.narrative"
      :key="view.version"
      :narrative="view.prompt.narrative"
      :busy="busy"
      :ms-per-char="msPerChar"
      @continue="emit('command', { kind: 'continue' })"
    />

    <div v-else-if="view.phase === 'ChoosingRoute' && view.prompt.routes" class="space-y-4">
      <OutpostPanel
        v-if="view.prompt.outpost"
        :outpost="view.prompt.outpost"
        :resources="view.resources"
        :busy="busy"
        :repair-in-progress="view.repairJobs.length > 0"
        @command="(c) => emit('command', c)"
      />
      <EmergencyFuelOffer
        v-if="view.prompt.emergencyFuel"
        :offer="view.prompt.emergencyFuel"
        :scrip="view.resources.scrip"
        :busy="busy"
        @hail="emit('command', { kind: 'hail' })"
      />
      <RoutePicker
        v-model:selected="selectedRoute"
        :routes="view.prompt.routes"
        :busy="busy"
        :has-repair-in-progress="view.repairJobs.length > 0"
        @depart="(nodeId) => emit('command', { kind: 'route', nodeId })"
      />
    </div>

    <ChallengePrompt
      v-else-if="view.phase === 'AwaitingResponse' && view.prompt.challenge"
      :key="`challenge-${view.version}`"
      :challenge="view.prompt.challenge"
      :busy="busy"
      :server-offset-ms="serverOffsetMs"
      @submit="(text) => emit('command', { kind: 'response', text })"
    />

    <DilemmaPrompt
      v-else-if="view.phase === 'AwaitingChoice' && view.prompt.dilemma"
      :dilemma="view.prompt.dilemma"
      :busy="busy"
      :server-offset-ms="serverOffsetMs"
      @choose="(optionId) => emit('command', { kind: 'choice', optionId })"
    />

    <SkirmishArena v-else-if="view.phase === 'Skirmish' && view.prompt.skirmish" :skirmish="view.prompt.skirmish" :busy="busy" @report="report" />

    <FinalePanel v-else-if="view.phase === 'Finished' && view.finale" :finale="view.finale" />
  </section>
</template>
