import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { gamesApi } from '@/api/games-api'
import { toApiError, type ApiError } from '@/api/http'
import type { CreateGameRequest, CreatedGameResponse, GameTone, SaveSlot, StorySeedResult } from '@/api/types'

/** The ten save slots (served from the Postgres read model). */
export const useSavesStore = defineStore('saves', () => {
  const slots = ref<SaveSlot[]>([])
  const loading = ref(false)
  const error = ref<ApiError | null>(null)

  const occupiedCount = computed(() => slots.value.filter((slot) => slot.game !== null).length)
  const firstFreeSlot = computed(() => slots.value.find((slot) => slot.game === null)?.slotNumber ?? null)

  async function load(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      slots.value = (await gamesApi.listSlots()).slots
    } catch (caught) {
      error.value = toApiError(caught)
    } finally {
      loading.value = false
    }
  }

  async function create(request: CreateGameRequest): Promise<CreatedGameResponse> {
    const created = await gamesApi.create(request)
    void load()
    return created
  }

  /** Optimistic delete: the slot empties immediately and is restored if the server refuses. */
  async function remove(gameId: string): Promise<void> {
    const snapshot = slots.value
    slots.value = slots.value.map((slot) => (slot.game?.gameId === gameId ? { ...slot, game: null } : slot))
    try {
      await gamesApi.remove(gameId)
    } catch (caught) {
      slots.value = snapshot
      throw toApiError(caught)
    }
  }

  function generateSeed(tone: GameTone | null, hint: string | null): Promise<StorySeedResult> {
    return gamesApi.generateSeed(tone, hint)
  }

  return { slots, loading, error, occupiedCount, firstFreeSlot, load, create, remove, generateSeed }
})
