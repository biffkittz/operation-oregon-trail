import type { GameSnapshot, GameView } from '@/api/types'

/** A minimal but complete GameView for tests; override what a test cares about. */
export function makeView(overrides: Partial<GameView> = {}): GameView {
  return {
    gameId: 'game-1',
    slotNumber: 1,
    version: 1,
    status: 'Active',
    phase: 'ChoosingRoute',
    title: 'Test Voyage',
    captainName: 'Ada',
    shipName: 'Mule',
    tone: 'Gritty',
    difficulty: 'Normal',
    storySeed: 'A seed.',
    day: 1,
    resources: { fuel: 50, rations: 50, hull: 90, morale: 70, scrip: 80, crew: 5 },
    stats: [],
    upgradePoints: 0,
    allies: [],
    map: { nodes: [], edges: [], currentNodeId: 'c0r0', destinationNodeId: 'c7r0', columns: 8 },
    journal: [],
    prompt: { narrative: null, routes: [], outpost: null, challenge: null, dilemma: null, skirmish: null, emergencyFuel: null },
    repairJobs: [],
    finale: null,
    updatedAt: new Date().toISOString(),
    ...overrides,
  }
}

export function makeSnapshot(view: Partial<GameView> = {}, serverTime = new Date().toISOString()): GameSnapshot {
  return { view: makeView(view), serverTime, source: 'live' }
}
