<script setup lang="ts">
/** Ship supplies and condition. */
import type { RepairJobView, ResourcesView } from '@/api/types'
import MeterBar from '@/components/ui/meter-bar.vue'
import RepairJobTimer from './repair-job-timer.vue'

interface Props {
  resources: ResourcesView
  day: number
  repairJobs: RepairJobView[]
  serverOffsetMs: number
}

defineProps<Props>()
</script>

<template>
  <section class="panel space-y-3" aria-labelledby="ship-status-title">
    <h2 id="ship-status-title" class="panel-title">Ship status</h2>
    <MeterBar label="Hull" :value="resources.hull" suffix="%" tone="sage" :low-at="30" />
    <MeterBar label="Fuel" :value="resources.fuel" tone="phosphor" :low-at="15" />
    <MeterBar label="Rations" :value="resources.rations" tone="phosphor" :low-at="15" />
    <MeterBar label="Morale" :value="resources.morale" tone="lavender" :low-at="20" />
    <dl class="grid grid-cols-3 gap-2 pt-1 font-display text-lg">
      <div><dt class="text-muted uppercase">Day</dt><dd class="text-2xl text-dust">{{ day }}</dd></div>
      <div><dt class="text-muted uppercase">Crew</dt><dd class="text-2xl text-dust">{{ resources.crew }}</dd></div>
      <div><dt class="text-muted uppercase">Scrip</dt><dd class="text-2xl text-phosphor">{{ resources.scrip }}</dd></div>
    </dl>
    <div v-if="repairJobs.length">
      <h3 class="font-display text-lg tracking-wider text-signal uppercase">Repairs underway</h3>
      <ul class="mt-1 space-y-1">
        <RepairJobTimer v-for="job in repairJobs" :key="job.id" :job="job" :server-offset-ms="serverOffsetMs" />
      </ul>
    </div>
  </section>
</template>
