import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { gamesApi } from '@/api/games-api'
import { ApiError } from '@/api/http'
import type { CreateGameRequest, CreatedGameResponse, GameTone, SaveSlot, SaveSlotsResponse, StorySeedResult } from '@/api/types'
import { useSavesStore } from '../saves'

vi.mock('@/api/games-api', () => ({
  gamesApi: {
    listSlots: vi.fn<() => Promise<SaveSlotsResponse>>(),
    remove: vi.fn<(gameId: string) => Promise<void>>(),
    create: vi.fn<(request: CreateGameRequest) => Promise<CreatedGameResponse>>(),
    generateSeed: vi.fn<(tone: GameTone | null, hint: string | null) => Promise<StorySeedResult>>(),
  },
}))

const occupied: SaveSlot = {
  slotNumber: 1,
  game: {
    gameId: 'g1',
    title: 'Voyage',
    captainName: 'Ada',
    shipName: 'Mule',
    tone: 'Gritty',
    difficulty: 'Normal',
    status: 'Active',
    phase: 'ChoosingRoute',
    day: 3,
    hull: 80,
    crew: 5,
    locationName: 'Somewhere',
    score: null,
    createdAt: '',
    updatedAt: '',
  },
}

describe('saves store', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('empties the slot immediately when scuttling', async () => {
    const saves = useSavesStore()
    saves.slots = [occupied, { slotNumber: 2, game: null }]
    vi.mocked(gamesApi.remove).mockResolvedValue()

    await saves.remove('g1')

    expect(saves.slots[0]?.game).toBeNull()
    expect(saves.firstFreeSlot).toBe(1)
  })

  it('rolls back the optimistic delete when the server refuses', async () => {
    const saves = useSavesStore()
    saves.slots = [occupied]
    vi.mocked(gamesApi.remove).mockRejectedValue(new ApiError(503, 'Game engine unavailable', 'Try again'))

    await expect(saves.remove('g1')).rejects.toBeInstanceOf(ApiError)
    expect(saves.slots[0]?.game?.gameId).toBe('g1')
  })
})
