import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { onScopeDispose, ref, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'
import type { GameSnapshot } from '@/api/types'

export type HubStatus = 'connecting' | 'live' | 'reconnecting' | 'offline'

/**
 * Subscribes to server pushes for one game over SignalR. Pushes carry full snapshots, so a missed message is
 * harmless: after a reconnect the caller simply refetches (`onReconnected`).
 */
export function useGameHub(
  gameId: MaybeRefOrGetter<string>,
  onUpdate: (snapshot: GameSnapshot) => void,
  onReconnected?: () => void,
): { status: Ref<HubStatus> } {
  const status = ref<HubStatus>('connecting')
  let disposed = false
  let retryTimer: ReturnType<typeof setTimeout> | undefined

  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/game')
    .withAutomaticReconnect([0, 2000, 5000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build()

  const join = (id: string) => connection.invoke('WatchGame', id)

  connection.on('GameUpdated', (snapshot: GameSnapshot) => onUpdate(snapshot))
  connection.onreconnecting(() => (status.value = 'reconnecting'))
  connection.onreconnected(async () => {
    status.value = 'live'
    await join(toValue(gameId))
    onReconnected?.()
  })
  connection.onclose(() => {
    status.value = 'offline'
    scheduleRestart()
  })

  async function start() {
    try {
      await connection.start()
      status.value = 'live'
      await join(toValue(gameId))
    } catch {
      status.value = 'offline'
      scheduleRestart()
    }
  }

  // Automatic reconnect gives up after its schedule; keep trying slowly while the screen is open.
  function scheduleRestart() {
    if (disposed || retryTimer !== undefined) return
    retryTimer = setTimeout(() => {
      retryTimer = undefined
      if (!disposed && connection.state === HubConnectionState.Disconnected) void start()
    }, 15_000)
  }

  watch(
    () => toValue(gameId),
    async (next, previous) => {
      if (connection.state !== HubConnectionState.Connected) return
      if (previous) await connection.invoke('UnwatchGame', previous)
      await join(next)
    },
  )

  void start()
  onScopeDispose(() => {
    disposed = true
    if (retryTimer !== undefined) clearTimeout(retryTimer)
    void connection.stop()
  })

  return { status }
}
