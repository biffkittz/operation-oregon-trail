import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { gamesApi, type CommandResult } from '@/api/games-api'
import { ApiError } from '@/api/http'
import type { GameCommand, GameSnapshot, JournalPage } from '@/api/types'
import { makeSnapshot } from '@/test-support/fixtures'
import { useGameStore } from '../game'

vi.mock('@/api/games-api', () => ({
  gamesApi: {
    get: vi.fn<(gameId: string, signal?: AbortSignal) => Promise<GameSnapshot>>(),
    command: vi.fn<(gameId: string, command: GameCommand, commandId: string) => Promise<CommandResult>>(),
    journal: vi.fn<(gameId: string, before: number, limit?: number) => Promise<JournalPage>>(),
  },
}))

describe('game store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(gamesApi.get).mockReset()
    vi.mocked(gamesApi.command).mockReset()
  })

  it('ignores snapshots older than the one on screen (pushes can arrive out of order)', () => {
    const game = useGameStore()
    expect(game.applySnapshot(makeSnapshot({ version: 5, day: 5 }))).toBe(true)
    expect(game.applySnapshot(makeSnapshot({ version: 4, day: 4 }))).toBe(false)
    expect(game.view?.day).toBe(5)
  })

  it('computes the server clock offset from each snapshot', () => {
    const game = useGameStore()
    game.applySnapshot(makeSnapshot({}, new Date(Date.now() + 3_000).toISOString()))
    expect(game.serverOffsetMs).toBeGreaterThan(2_500)
    expect(game.serverOffsetMs).toBeLessThan(3_500)
  })

  it('stays busy after a 202 until a newer version is pushed', async () => {
    const game = useGameStore()
    game.applySnapshot(makeSnapshot({ version: 3 }))
    vi.mocked(gamesApi.command).mockResolvedValue({ status: 'pending', message: 'still working' })

    await game.send({ kind: 'continue' })
    expect(game.busy).toBe(true)
    expect(game.pendingMessage).toBe('still working')

    game.applySnapshot(makeSnapshot({ version: 3 }))
    expect(game.busy).toBe(true)

    game.applySnapshot(makeSnapshot({ version: 4 }))
    expect(game.busy).toBe(false)
    expect(game.pendingMessage).toBeNull()
  })

  it('sends a unique command id with every command', async () => {
    const game = useGameStore()
    game.applySnapshot(makeSnapshot({ version: 1 }))
    vi.mocked(gamesApi.command).mockResolvedValue({ status: 'done', snapshot: makeSnapshot({ version: 2 }) })

    await game.send({ kind: 'continue' })
    await game.send({ kind: 'route', nodeId: 'c1r0' })

    const ids = vi.mocked(gamesApi.command).mock.calls.map((call) => call[2])
    expect(new Set(ids).size).toBe(2)
  })

  it('surfaces a refused command as an error and unlocks input', async () => {
    const game = useGameStore()
    game.applySnapshot(makeSnapshot({ version: 1 }))
    vi.mocked(gamesApi.command).mockRejectedValue(new ApiError(409, 'Command refused', "There's nothing to acknowledge right now."))

    const ok = await game.send({ kind: 'continue' })

    expect(ok).toBe(false)
    expect(game.busy).toBe(false)
    expect(game.error?.detail).toContain('nothing to acknowledge')
  })

  it('merges older journal pages without duplicates', async () => {
    const game = useGameStore()
    const entry = (sequence: number) => ({ sequence, day: 1, kind: 'Event' as const, text: `#${sequence}`, at: '' })
    game.applySnapshot(makeSnapshot({ journal: [entry(3), entry(4)] }))
    vi.mocked(gamesApi.journal).mockResolvedValue({ entries: [entry(1), entry(2), entry(3)], hasMore: false })

    await game.loadOlderJournal()

    expect(game.journal.map((e) => e.sequence)).toEqual([1, 2, 3, 4])
    expect(game.hasOlderJournal).toBe(false)
  })
})
