import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { gamesApi } from '@/api/games-api'
import { toApiError, type ApiError } from '@/api/http'
import type { GameCommand, GameSnapshot, GameView, JournalEntryView } from '@/api/types'

/** If a 202 "still working" never gets its SignalR push (e.g. the socket dropped), refetch after this long. */
const PendingRefetchMs = 20_000

function isAbort(error: unknown): boolean {
  return error instanceof Error && (error.name === 'CanceledError' || error.name === 'AbortError')
}

/**
 * The active game. State arrives from three places (command responses, SignalR pushes, refetches) in any
 * order; `version` makes them converge: an older snapshot never overwrites a newer one.
 */
export const useGameStore = defineStore('game', () => {
  const view = ref<GameView | null>(null)
  const source = ref<'live' | 'archive'>('live')
  /** Server clock minus browser clock, used by countdowns. */
  const serverOffsetMs = ref(0)
  const loading = ref(false)
  /** A command is in flight (or accepted and awaiting its push); the UI locks input meanwhile. */
  const busy = ref(false)
  const pendingMessage = ref<string | null>(null)
  const error = ref<ApiError | null>(null)
  const olderJournal = ref<JournalEntryView[]>([])
  /** False once the read model reported no more pages. */
  const serverHasOlder = ref(true)

  let loadController: AbortController | null = null
  let awaitingVersionAbove: number | null = null
  let pendingTimer: ReturnType<typeof setTimeout> | undefined

  const phase = computed(() => view.value?.phase ?? 'Initializing')
  const isActive = computed(() => view.value?.status === 'Active')

  /** Recent entries from the view plus older pages from the read model, de-duplicated and ordered. */
  const journal = computed<JournalEntryView[]>(() => {
    const bySequence = new Map<number, JournalEntryView>()
    for (const entry of [...olderJournal.value, ...(view.value?.journal ?? [])]) bySequence.set(entry.sequence, entry)
    return [...bySequence.values()].sort((a, b) => a.sequence - b.sequence)
  })

  /** Sequences start at 1, so a log that already shows entry #1 has nothing older to fetch. */
  const hasOlderJournal = computed(() => serverHasOlder.value && (journal.value[0]?.sequence ?? 1) > 1)

  function clearPending() {
    awaitingVersionAbove = null
    pendingMessage.value = null
    busy.value = false
    if (pendingTimer !== undefined) clearTimeout(pendingTimer)
    pendingTimer = undefined
  }

  /** Applies a snapshot unless it is older than what we already show. Returns whether it was applied. */
  function applySnapshot(snapshot: GameSnapshot): boolean {
    const current = view.value
    if (current && current.gameId === snapshot.view.gameId && snapshot.view.version < current.version) {
      return false
    }
    view.value = snapshot.view
    source.value = snapshot.source
    serverOffsetMs.value = new Date(snapshot.serverTime).getTime() - Date.now()
    if (awaitingVersionAbove !== null && snapshot.view.version > awaitingVersionAbove) {
      clearPending()
    }
    return true
  }

  async function load(gameId: string): Promise<void> {
    // Switching games cancels the previous request so a slow response cannot paint the wrong voyage.
    loadController?.abort()
    const controller = new AbortController()
    loadController = controller
    if (view.value?.gameId !== gameId) {
      view.value = null
      olderJournal.value = []
      serverHasOlder.value = true
      clearPending()
    }
    loading.value = true
    error.value = null
    try {
      applySnapshot(await gamesApi.get(gameId, controller.signal))
    } catch (caught) {
      if (!isAbort(caught)) error.value = toApiError(caught)
    } finally {
      if (loadController === controller) loading.value = false
    }
  }

  async function send(command: GameCommand): Promise<boolean> {
    const current = view.value
    if (!current || busy.value) return false
    busy.value = true
    error.value = null
    try {
      const result = await gamesApi.command(current.gameId, command, crypto.randomUUID())
      if (result.status === 'done') {
        applySnapshot(result.snapshot)
        clearPending()
      } else {
        // 202: the turn is still running (slow LLM). Stay busy until a newer version is pushed.
        awaitingVersionAbove = current.version
        pendingMessage.value = result.message
        pendingTimer = setTimeout(() => void load(current.gameId), PendingRefetchMs)
      }
      return true
    } catch (caught) {
      error.value = toApiError(caught)
      clearPending()
      return false
    }
  }

  async function loadOlderJournal(): Promise<void> {
    const current = view.value
    const oldest = journal.value[0]
    if (!current || !oldest || !hasOlderJournal.value) return
    try {
      const page = await gamesApi.journal(current.gameId, oldest.sequence)
      olderJournal.value = [...page.entries, ...olderJournal.value]
      serverHasOlder.value = page.hasMore
    } catch (caught) {
      error.value = toApiError(caught)
    }
  }

  function reset() {
    loadController?.abort()
    view.value = null
    olderJournal.value = []
    serverHasOlder.value = true
    error.value = null
    clearPending()
  }

  return {
    view,
    source,
    serverOffsetMs,
    loading,
    busy,
    pendingMessage,
    error,
    hasOlderJournal,
    phase,
    isActive,
    journal,
    applySnapshot,
    load,
    send,
    loadOlderJournal,
    reset,
  }
})
