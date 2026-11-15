import { http } from './http'
import type {
  CreateGameRequest,
  CreatedGameResponse,
  GameCommand,
  GameSnapshot,
  GameTone,
  JournalPage,
  SaveSlotsResponse,
  StorySeedResult,
} from './types'

/** Result of a command: either the new snapshot (200) or "still running, watch SignalR" (202). */
export type CommandResult = { status: 'done'; snapshot: GameSnapshot } | { status: 'pending'; message: string }

function commandBody(command: GameCommand): Record<string, unknown> | undefined {
  const { kind, ...body } = command
  return kind === 'continue' ? undefined : body
}

export const gamesApi = {
  async listSlots(): Promise<SaveSlotsResponse> {
    const { data } = await http.get<SaveSlotsResponse>('/api/games')
    return data
  },

  async create(request: CreateGameRequest): Promise<CreatedGameResponse> {
    const { data } = await http.post<CreatedGameResponse>('/api/games', request)
    return data
  },

  async get(gameId: string, signal?: AbortSignal): Promise<GameSnapshot> {
    const { data } = await http.get<GameSnapshot>(`/api/games/${gameId}`, { signal })
    return data
  },

  async journal(gameId: string, before: number, limit = 50): Promise<JournalPage> {
    const { data } = await http.get<JournalPage>(`/api/games/${gameId}/journal`, { params: { before, limit } })
    return data
  },

  async remove(gameId: string): Promise<void> {
    await http.delete(`/api/games/${gameId}`)
  },

  /**
   * Sends a command. The command ID becomes the Temporal update ID, so retrying the same ID after a network
   * blip can never apply the command twice.
   */
  async command(gameId: string, command: GameCommand, commandId: string): Promise<CommandResult> {
    const response = await http.post<GameSnapshot | { message: string }>(
      `/api/games/${gameId}/${command.kind}`,
      commandBody(command),
      { headers: { 'X-Command-Id': commandId } },
    )
    return response.status === 202
      ? { status: 'pending', message: (response.data as { message: string }).message }
      : { status: 'done', snapshot: response.data as GameSnapshot }
  },

  async generateSeed(tone: GameTone | null, hint: string | null): Promise<StorySeedResult> {
    const { data } = await http.post<StorySeedResult>('/api/story-seeds', { tone, hint })
    return data
  },
}
