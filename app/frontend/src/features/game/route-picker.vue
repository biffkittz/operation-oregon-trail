<script setup lang="ts">
/** Choose the next leg. Mirrors the map selection so keyboard and screen-reader users have a plain list. */
import { computed } from 'vue'
import type { RouteOptionView } from '@/api/types'
import RetroButton from '@/components/ui/retro-button.vue'
import { nodeKindInfo } from '@/lib/game-labels'

interface Props {
  routes: RouteOptionView[]
  busy: boolean
  hasRepairInProgress: boolean
}

const { routes, busy, hasRepairInProgress } = defineProps<Props>()
const selected = defineModel<string | null>('selected', { required: true })
const emit = defineEmits<{ depart: [nodeId: string] }>()

const choice = computed(() => routes.find((r) => r.nodeId === selected.value) ?? null)
</script>

<template>
  <div class="space-y-3">
    <fieldset>
      <legend class="field-label">Set a course</legend>
      <ul class="space-y-2">
        <li v-for="route in routes" :key="route.nodeId">
          <label
            class="flex cursor-pointer items-center gap-3 rounded-sm border-2 px-3 py-2 has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-signal"
            :class="[
              selected === route.nodeId ? 'border-signal bg-signal/10' : 'border-edge hover:border-signal-dim',
              route.affordable ? '' : 'cursor-not-allowed opacity-50',
            ]"
          >
            <input v-model="selected" class="sr-only" type="radio" name="route" :value="route.nodeId" :disabled="!route.affordable" />
            <span class="w-6 text-center font-display text-2xl text-phosphor" aria-hidden="true">{{ nodeKindInfo[route.kind].glyph }}</span>
            <span class="flex-1">
              <span class="block font-display text-xl text-dust">{{ route.name }}</span>
              <span class="block text-sm text-muted">
                {{ nodeKindInfo[route.kind].label }} · {{ route.days }} {{ route.days === 1 ? 'day' : 'days' }} · {{ route.fuelCost }} fuel ·
                {{ route.rationsCost }} rations<template v-if="!route.affordable"> · not enough fuel</template>
              </span>
              <span v-if="route.warning" class="mt-1 block text-sm text-rust">
                <span aria-hidden="true">⚠ </span><span class="sr-only">Warning: </span>{{ route.warning }}
              </span>
            </span>
          </label>
        </li>
      </ul>
    </fieldset>
    <p v-if="hasRepairInProgress" class="text-sm text-phosphor">Leaving now abandons the repair in progress.</p>
    <RetroButton :disabled="!choice || !choice.affordable" :busy="busy" @click="choice && emit('depart', choice.nodeId)">
      {{ choice ? `Head for ${choice.name}` : 'Pick a destination' }}
    </RetroButton>
  </div>
</template>
