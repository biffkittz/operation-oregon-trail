<script setup lang="ts">
/** Outpost services: supplies, timed repairs and haggling with the smith (a persuasion challenge). */
import { ref } from 'vue'
import type { GameCommand, OutpostPromptView, ResourcesView } from '@/api/types'
import RetroButton from '@/components/ui/retro-button.vue'

interface Props {
  outpost: OutpostPromptView
  resources: ResourcesView
  busy: boolean
  repairInProgress: boolean
}

const { outpost, resources, busy, repairInProgress } = defineProps<Props>()
const emit = defineEmits<{ command: [command: GameCommand] }>()

const fuelUnits = ref(1)
const rationUnits = ref(1)

function buy(service: 'BuyFuel' | 'BuyRations', quantity: number) {
  emit('command', { kind: 'outpost', service, quantity })
}
</script>

<template>
  <section class="rounded-sm border border-edge bg-panel-raised p-3" aria-labelledby="outpost-title">
    <h3 id="outpost-title" class="font-display text-2xl text-signal">{{ outpost.name }} services</h3>
    <div class="mt-3 grid gap-3 sm:grid-cols-2">
      <div class="space-y-2">
        <label class="flex items-center justify-between gap-2">
          <span class="text-sm text-dust">Fuel ({{ outpost.fuelPrice }} scrip / 10)</span>
          <input v-model.number="fuelUnits" type="number" min="1" max="10" class="field-input w-16 py-1" aria-label="Fuel units of ten" />
        </label>
        <RetroButton
          variant="secondary"
          class="w-full"
          :disabled="busy || resources.fuel >= 100 || resources.scrip < outpost.fuelPrice * fuelUnits"
          @click="buy('BuyFuel', fuelUnits)"
        >
          Buy {{ fuelUnits * 10 }} fuel · {{ outpost.fuelPrice * fuelUnits }}₴
        </RetroButton>
      </div>
      <div class="space-y-2">
        <label class="flex items-center justify-between gap-2">
          <span class="text-sm text-dust">Rations ({{ outpost.rationsPrice }} scrip / 10)</span>
          <input v-model.number="rationUnits" type="number" min="1" max="10" class="field-input w-16 py-1" aria-label="Ration units of ten" />
        </label>
        <RetroButton
          variant="secondary"
          class="w-full"
          :disabled="busy || resources.rations >= 100 || resources.scrip < outpost.rationsPrice * rationUnits"
          @click="buy('BuyRations', rationUnits)"
        >
          Buy {{ rationUnits * 10 }} rations · {{ outpost.rationsPrice * rationUnits }}₴
        </RetroButton>
      </div>
    </div>
    <div class="mt-3 flex flex-wrap items-center gap-3 border-t border-edge pt-3">
      <p class="flex-1 text-sm text-dust">
        The smith patches {{ outpost.repairAmount }}% hull for {{ outpost.repairPrice }} scrip in {{ outpost.repairSeconds }} seconds.
        <span v-if="outpost.repairDiscountPercent > 0" class="text-sage">({{ outpost.repairDiscountPercent }}% haggled off)</span>
        <span v-else-if="outpost.repairDiscountPercent < 0" class="text-rust">(+{{ -outpost.repairDiscountPercent }}%: she's offended)</span>
      </p>
      <RetroButton
        variant="secondary"
        :disabled="busy || repairInProgress || resources.hull >= 100 || resources.scrip < outpost.repairPrice"
        @click="emit('command', { kind: 'outpost', service: 'CommissionRepair' })"
      >
        Commission repair
      </RetroButton>
      <RetroButton
        v-if="outpost.haggleAvailable"
        variant="ghost"
        :disabled="busy || repairInProgress"
        @click="emit('command', { kind: 'outpost', service: 'Haggle' })"
      >
        Haggle
      </RetroButton>
    </div>
  </section>
</template>
