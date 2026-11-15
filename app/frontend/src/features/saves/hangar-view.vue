<script setup lang="ts">
/** Save-slot screen: up to ten voyages, each a long-running Temporal workflow. */
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import ConfirmDialog from '@/components/ui/confirm-dialog.vue'
import LoadingCursor from '@/components/ui/loading-cursor.vue'
import RetroButton from '@/components/ui/retro-button.vue'
import { useNotificationsStore } from '@/stores/notifications'
import { useSavesStore } from '@/stores/saves'
import SaveSlotCard from './save-slot-card.vue'

const saves = useSavesStore()
const notifications = useNotificationsStore()
const router = useRouter()

const confirmOpen = ref(false)
const pendingScuttle = ref<{ gameId: string; title: string } | null>(null)

onMounted(() => void saves.load())

function askScuttle(gameId: string, title: string) {
  pendingScuttle.value = { gameId, title }
  confirmOpen.value = true
}

async function scuttle() {
  const target = pendingScuttle.value
  if (!target) return
  try {
    await saves.remove(target.gameId)
    notifications.push('success', `${target.title} was scuttled. The berth is free.`)
  } catch {
    notifications.push('error', `Could not scuttle ${target.title}. Try again.`)
  }
}
</script>

<template>
  <section aria-labelledby="hangar-title">
    <div class="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 id="hangar-title" class="glow font-display text-5xl">The Hangar</h1>
        <p class="text-dust">{{ saves.occupiedCount }} of {{ saves.slots.length || 10 }} berths occupied. Each wagon keeps its own voyage.</p>
      </div>
      <RetroButton
        v-if="saves.firstFreeSlot !== null"
        @click="router.push({ name: 'new-game', query: { slot: saves.firstFreeSlot } })"
      >
        New voyage
      </RetroButton>
    </div>

    <LoadingCursor v-if="saves.loading && saves.slots.length === 0" label="Counting wagons" />
    <div v-else-if="saves.error" role="alert" class="panel border-rust text-rust">
      {{ saves.error.detail }}
      <RetroButton variant="secondary" class="ml-4" @click="saves.load()">Retry</RetroButton>
    </div>
    <ul v-else class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3" aria-label="Save slots">
      <li v-for="slot in saves.slots" :key="slot.slotNumber" class="flex">
        <SaveSlotCard
          class="w-full"
          :entry="slot"
          @resume="(id) => router.push({ name: 'game', params: { gameId: id } })"
          @create="(n) => router.push({ name: 'new-game', query: { slot: n } })"
          @scuttle="askScuttle"
        />
      </li>
    </ul>

    <ConfirmDialog v-model:open="confirmOpen" title="Scuttle this voyage?" confirm-label="Scuttle" danger @confirm="scuttle">
      <p>
        <strong class="text-phosphor">{{ pendingScuttle?.title }}</strong> will be ended for good and its berth freed. The crew will
        not thank you.
      </p>
    </ConfirmDialog>
  </section>
</template>
