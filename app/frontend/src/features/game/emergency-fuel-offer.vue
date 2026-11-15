<script setup lang="ts">
/**
 * Shown when the ship is stranded at a stop with no fuel depot: every route is out of reach, but a passing hauler
 * will sell exactly enough fuel for the shortest one, at a steep price and after days adrift. Without this offer
 * the voyage would simply end.
 */
import type { EmergencyFuelOfferView } from '@/api/types'
import RetroButton from '@/components/ui/retro-button.vue'

interface Props {
  offer: EmergencyFuelOfferView
  scrip: number
  busy: boolean
}

const { offer, scrip, busy } = defineProps<Props>()
const emit = defineEmits<{ hail: [] }>()
</script>

<template>
  <section class="panel space-y-3 border-rust" aria-labelledby="emergency-fuel-heading">
    <h3 id="emergency-fuel-heading" class="font-display text-2xl text-rust">Stranded</h3>
    <p class="text-dust">
      The tanks are too low for any route and there's no fuel depot here. Light the beacon and wait for a passing hauler:
      {{ offer.waitDays }} {{ offer.waitDays === 1 ? 'day' : 'days' }} adrift (the crew keeps eating and grumbling), then
      {{ offer.fuel }} fuel for {{ offer.price }} scrip.
    </p>
    <p class="text-sm text-muted">You have {{ scrip }} scrip.</p>
    <RetroButton variant="danger" :busy="busy" @click="emit('hail')">Hail a hauler ({{ offer.price }} scrip)</RetroButton>
  </section>
</template>
