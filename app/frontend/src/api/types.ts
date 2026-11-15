/**
 * TypeScript mirrors of the backend DTOs (Oregon.Contracts and Oregon.Backend). Property names are camelCase
 * as serialized by ASP.NET; enums are serialized as their C# names.
 * Keep in sync with shared/Oregon.Contracts/Model and app/backend/Games.
 */

export type GameTone = 'Whimsical' | 'Hopeful' | 'Gritty' | 'Morose'
export type Difficulty = 'Easy' | 'Normal' | 'Hard'
export type GamePhase =
  | 'Initializing'
  | 'Narrative'
  | 'ChoosingRoute'
  | 'AwaitingResponse'
  | 'AwaitingChoice'
  | 'Skirmish'
  | 'Finished'
export type GameStatus = 'Active' | 'Victory' | 'Defeat'
export type StatKind = 'Creativity' | 'Morality' | 'Resilience' | 'Diplomacy' | 'Ingenuity' | 'Reflexes'
export type NodeKind =
  | 'Origin'
  | 'Outpost'
  | 'Settlement'
  | 'Derelict'
  | 'Hazard'
  | 'Raiders'
  | 'Anomaly'
  | 'Landmark'
  | 'Destination'
export type JournalEntryKind = 'Narration' | 'Player' | 'Npc' | 'Outcome' | 'Event' | 'System'
export type Verdict = 'CriticalSuccess' | 'Success' | 'Partial' | 'Failure' | 'CriticalFailure'
export type ChallengeKind = 'Persuasion' | 'Ingenuity' | 'Creativity' | 'Morality' | 'Hardship' | 'Haggle'
export type OutpostService = 'BuyFuel' | 'BuyRations' | 'CommissionRepair' | 'Haggle'
export type TextSpeed = 'Instant' | 'Fast' | 'Normal' | 'Slow'

export interface ResourcesView {
  fuel: number
  rations: number
  hull: number
  morale: number
  scrip: number
  crew: number
}

export interface StatView {
  stat: StatKind
  level: number
  xp: number
  xpToNext: number
}

export interface AllyView {
  name: string
  role: string
  perk: string
}

export interface MapNodeView {
  id: string
  name: string
  kind: NodeKind
  column: number
  row: number
  rowsInColumn: number
  description: string
  visited: boolean
  reachable: boolean
}

export interface MapEdgeView {
  from: string
  to: string
  days: number
}

export interface MapView {
  nodes: MapNodeView[]
  edges: MapEdgeView[]
  currentNodeId: string
  destinationNodeId: string
  columns: number
}

export interface JournalEntryView {
  sequence: number
  day: number
  kind: JournalEntryKind
  text: string
  at: string
}

export interface RepairJobView {
  id: string
  label: string
  hullAmount: number
  readyAt: string
}

export interface ResourceChangeView {
  label: string
  delta: number
}

export interface NarrativePromptView {
  headline: string
  text: string
  verdict: Verdict | null
  changes: ResourceChangeView[]
  continueLabel: string
}

export interface RouteOptionView {
  nodeId: string
  name: string
  kind: NodeKind
  days: number
  fuelCost: number
  rationsCost: number
  affordable: boolean
}

export interface OutpostPromptView {
  name: string
  fuelPrice: number
  rationsPrice: number
  repairPrice: number
  repairAmount: number
  repairSeconds: number
  haggleAvailable: boolean
  repairDiscountPercent: number
}

export interface ChallengePromptView {
  kind: ChallengeKind
  stat: StatKind
  title: string
  description: string
  npcName: string | null
  npcLine: string | null
  stakes: string
  deadline: string | null
  maxResponseLength: number
}

export interface DilemmaOptionView {
  id: string
  label: string
  hint: string
}

export interface DilemmaPromptView {
  title: string
  description: string
  options: DilemmaOptionView[]
  deadline: string | null
}

export interface SkirmishSpecView {
  seed: number
  targets: number
  durationSeconds: number
  speedFactor: number
  targetSizePx: number
}

export interface SkirmishPromptView {
  title: string
  description: string
  spec: SkirmishSpecView
}

export interface PromptView {
  narrative: NarrativePromptView | null
  routes: RouteOptionView[] | null
  outpost: OutpostPromptView | null
  challenge: ChallengePromptView | null
  dilemma: DilemmaPromptView | null
  skirmish: SkirmishPromptView | null
}

export interface FinaleView {
  outcome: GameStatus
  score: number
  days: number
  epilogue: string
}

export interface GameView {
  gameId: string
  slotNumber: number
  version: number
  status: GameStatus
  phase: GamePhase
  title: string
  captainName: string
  shipName: string
  tone: GameTone
  difficulty: Difficulty
  storySeed: string
  day: number
  resources: ResourcesView
  stats: StatView[]
  upgradePoints: number
  allies: AllyView[]
  map: MapView
  journal: JournalEntryView[]
  prompt: PromptView
  repairJobs: RepairJobView[]
  finale: FinaleView | null
  updatedAt: string
}

export interface GameSnapshot {
  view: GameView
  serverTime: string
  source: 'live' | 'archive'
}

export interface SavedGameSummary {
  gameId: string
  title: string
  captainName: string
  shipName: string
  tone: GameTone
  difficulty: Difficulty
  status: GameStatus
  phase: GamePhase
  day: number
  hull: number
  crew: number
  locationName: string
  score: number | null
  createdAt: string
  updatedAt: string
}

export interface SaveSlot {
  slotNumber: number
  game: SavedGameSummary | null
}

export interface SaveSlotsResponse {
  slots: SaveSlot[]
  maxSlots: number
}

export interface CreateGameRequest {
  slotNumber?: number | null
  captainName: string
  shipName: string
  title?: string | null
  tone?: GameTone | null
  difficulty?: Difficulty | null
  storySeed: string
}

export interface CreatedGameResponse {
  gameId: string
  slotNumber: number
}

export interface StorySeedResult {
  title: string
  seed: string
}

export interface JournalPage {
  entries: JournalEntryView[]
  hasMore: boolean
}

export interface Settings {
  defaultTone: GameTone
  defaultDifficulty: Difficulty
  crtEffects: boolean
  textSpeed: TextSpeed
}

/** Union of every player command the game screen can send. */
export type GameCommand =
  | { kind: 'continue' }
  | { kind: 'route'; nodeId: string }
  | { kind: 'response'; text: string }
  | { kind: 'choice'; optionId: string }
  | { kind: 'skirmish'; hits: number; shotsFired: number; elapsedMs: number }
  | { kind: 'outpost'; service: OutpostService; quantity?: number }
  | { kind: 'upgrade'; stat: StatKind }

/** Limits shared with the backend (Oregon.Contracts.GameLimits). */
export const GameLimits = {
  maxSaveSlots: 10,
  maxResponseLength: 600,
  maxStorySeedLength: 1200,
  maxNameLength: 40,
} as const
