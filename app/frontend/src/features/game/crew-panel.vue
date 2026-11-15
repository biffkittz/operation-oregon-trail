<script setup lang="ts">
/** Captain attributes, upgrade points and allies. */
import type { AllyView, StatKind, StatView } from '@/api/types'
import MeterBar from '@/components/ui/meter-bar.vue'
import { statInfo } from '@/lib/game-labels'

interface Props {
  stats: StatView[]
  allies: AllyView[]
  upgradePoints: number
  canUpgrade: boolean
}

const { stats, allies, upgradePoints, canUpgrade } = defineProps<Props>()
const emit = defineEmits<{ upgrade: [stat: StatKind] }>()
</script>

<template>
  <section class="panel space-y-3" aria-labelledby="crew-title">
    <div class="flex items-baseline justify-between">
      <h2 id="crew-title" class="panel-title mb-0">Captain</h2>
      <p v-if="upgradePoints > 0" class="font-display text-lg text-phosphor">{{ upgradePoints }} upgrade {{ upgradePoints === 1 ? 'point' : 'points' }}</p>
    </div>
    <ul class="space-y-2">
      <li v-for="stat in stats" :key="stat.stat" class="flex items-center gap-2">
        <div class="flex-1" :title="statInfo[stat.stat].blurb">
          <MeterBar
            :label="`${statInfo[stat.stat].label} · Lv ${stat.level}`"
            :value="stat.xpToNext === 0 ? 1 : stat.xp"
            :max="stat.xpToNext === 0 ? 1 : stat.xpToNext"
            tone="signal"
            :display="stat.xpToNext === 0 ? 'mastered' : `${stat.xp}/${stat.xpToNext} xp`"
          />
        </div>
        <button
          v-if="upgradePoints > 0 && stat.xpToNext > 0"
          type="button"
          class="mt-4 h-8 w-8 shrink-0 rounded-sm border-2 border-phosphor font-display text-xl text-phosphor hover:bg-phosphor/15 disabled:opacity-40"
          :disabled="!canUpgrade"
          :aria-label="`Spend an upgrade point on ${statInfo[stat.stat].label}`"
          @click="emit('upgrade', stat.stat)"
        >
          +
        </button>
      </li>
    </ul>
    <div>
      <h3 class="font-display text-lg tracking-wider text-signal uppercase">Allies</h3>
      <p v-if="allies.length === 0" class="text-sm text-muted">Nobody yet. Impress the right people.</p>
      <ul v-else class="mt-1 space-y-1">
        <li v-for="ally in allies" :key="ally.name" class="text-sm">
          <span class="text-dust">{{ ally.name }}</span>
          <span class="text-muted">, {{ ally.role }}:</span>
          <span class="text-sage"> {{ ally.perk }}</span>
        </li>
      </ul>
    </div>
  </section>
</template>
