<script setup lang="ts">
/**
 * The click battle. Drones are real <button>s, so the fight works with mouse, touch and keyboard: in
 * keyboard mode each visible drone shows a number and pressing that digit fires at it. With reduced motion
 * the drones hold still. Results are reported automatically when the battle ends.
 */
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { SkirmishPromptView } from '@/api/types'
import RetroButton from '@/components/ui/retro-button.vue'
import { usePrefersReducedMotion } from '@/composables/usePrefersReducedMotion'
import { useSkirmish } from '@/composables/useSkirmish'
import { clock } from '@/lib/format'
import type { SkirmishTally } from '@/lib/skirmish'

interface Props {
  skirmish: SkirmishPromptView
  busy: boolean
}

const { skirmish, busy } = defineProps<Props>()
const emit = defineEmits<{ report: [tally: SkirmishTally] }>()

const reducedMotion = usePrefersReducedMotion()
const keyboardMode = ref(false)
const lastTally = ref<SkirmishTally | null>(null)

const battle = useSkirmish(
  () => skirmish.spec,
  (tally) => {
    lastTally.value = tally
    emit('report', tally)
  },
  { still: reducedMotion },
)

const liveDrones = computed(() => battle.drones.value.filter((d) => d.alive))

function onKey(event: KeyboardEvent) {
  if (!battle.running.value) return
  const digit = Number(event.key)
  if (Number.isInteger(digit) && digit >= 1 && digit <= 9) {
    event.preventDefault()
    keyboardMode.value = true
    battle.fireLabel(digit)
  }
}

watch(battle.running, (running) => {
  if (running) window.addEventListener('keydown', onKey)
  else window.removeEventListener('keydown', onKey)
})
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <section class="space-y-4" aria-labelledby="skirmish-title">
    <h3 id="skirmish-title" class="glow font-display text-4xl">{{ skirmish.title }}</h3>
    <p class="text-lg leading-relaxed text-dust">{{ skirmish.description }}</p>

    <div v-if="!battle.running.value && !battle.finished.value" class="space-y-3">
      <p class="text-sm text-muted">
        {{ skirmish.spec.targets }} drones, {{ skirmish.spec.durationSeconds }} seconds. Click or tap drones to fire, or press the number shown on
        a drone. Every shot counts toward accuracy, and the better you shoot, the bigger the salvage.
      </p>
      <RetroButton @click="battle.start()">Man the scatter cannon</RetroButton>
    </div>

    <div v-else class="space-y-2">
      <div class="flex items-center justify-between font-display text-2xl tabular-nums" role="status" aria-live="off">
        <span class="text-phosphor">{{ clock(battle.secondsLeft.value) }}</span>
        <span class="text-sage">Hits {{ battle.hits.value }}/{{ skirmish.spec.targets }}</span>
        <span class="text-muted">Shots {{ battle.shotsFired.value }}</span>
      </div>
      <!-- Clicking open space is a miss; drones stop propagation so a hit isn't also counted as a miss. -->
      <div
        class="relative aspect-video w-full overflow-hidden rounded-sm border-2 border-edge bg-[radial-gradient(circle_at_30%_20%,#1b2633_0%,#07090c_70%)]"
        role="application"
        aria-label="Skirmish arena. Press the number on a drone to fire at it."
        @click="battle.miss()"
      >
        <button
          v-for="drone in liveDrones"
          :key="drone.id"
          type="button"
          class="absolute flex -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full border-2 border-rust bg-rust/20 font-display text-xl text-rust hover:bg-rust/40"
          :style="{
            left: `${drone.x * 100}%`,
            top: `${drone.y * 100}%`,
            width: `${skirmish.spec.targetSizePx}px`,
            height: `${skirmish.spec.targetSizePx}px`,
          }"
          :aria-label="drone.label ? `Drone ${drone.label}` : 'Drone'"
          @click.stop="battle.fireAt(drone.id)"
        >
          <span aria-hidden="true">{{ keyboardMode && drone.label ? drone.label : '✦' }}</span>
        </button>
        <p v-if="battle.finished.value" class="absolute inset-0 flex items-center justify-center bg-void/70 font-display text-3xl text-phosphor" role="status">
          {{ busy ? 'Tallying the wreckage...' : 'Cease fire!' }}
        </p>
      </div>
      <p v-if="lastTally" class="text-sm text-muted">Reported {{ lastTally.hits }} hits from {{ lastTally.shotsFired }} shots.</p>
    </div>
  </section>
</template>
