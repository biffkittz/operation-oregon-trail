import { mulberry32 } from './prng'
import type { SkirmishSpecView } from '@/api/types'

/**
 * Pure simulation for the click-battle minigame, separated from rendering so it can be unit tested.
 * Coordinates are in arena units (0..1 on both axes); the component scales them to pixels.
 */
export interface Drone {
  id: number
  /** Number shown in keyboard mode (1-9); drones beyond nine are only clickable. */
  label: number
  spawnAtMs: number
  x: number
  y: number
  vx: number
  vy: number
  alive: boolean
  hit: boolean
}

export interface SkirmishTally {
  hits: number
  shotsFired: number
  elapsedMs: number
}

/** Visible drones at once; keeps keyboard mode playable (labels 1-9). */
export const MaxConcurrentDrones = 6

/**
 * Creates the drone wave. `still` (used when the OS requests reduced motion) keeps drones stationary: they
 * still appear and vanish on schedule, so the battle stays playable without movement.
 */
export function createDrones(spec: SkirmishSpecView, options: { still?: boolean } = {}): Drone[] {
  const random = mulberry32(spec.seed)
  const durationMs = spec.durationSeconds * 1000
  // Spawn evenly through the first 80% of the battle with a little jitter, so late drones are still hittable.
  const window = durationMs * 0.8
  return Array.from({ length: spec.targets }, (_, id) => {
    const angle = random() * Math.PI * 2
    const speed = options.still ? 0 : (0.08 + random() * 0.1) * spec.speedFactor
    return {
      id,
      label: 0,
      spawnAtMs: Math.round((window / spec.targets) * id + random() * 400),
      x: 0.1 + random() * 0.8,
      y: 0.15 + random() * 0.7,
      vx: Math.cos(angle) * speed,
      vy: Math.sin(angle) * speed,
      alive: false,
      hit: false,
    }
  })
}

/** Advances the simulation to `nowMs` (time since battle start). Drones bounce off the arena edges. */
export function step(drones: Drone[], previousMs: number, nowMs: number): void {
  const dt = Math.max(0, nowMs - previousMs) / 1000
  for (const drone of drones) {
    if (!drone.hit && !drone.alive && nowMs >= drone.spawnAtMs && activeCount(drones) < MaxConcurrentDrones) {
      drone.alive = true
    }
    if (!drone.alive) continue
    drone.x += drone.vx * dt
    drone.y += drone.vy * dt
    if (drone.x < 0.04 || drone.x > 0.96) {
      drone.vx = -drone.vx
      drone.x = Math.min(0.96, Math.max(0.04, drone.x))
    }
    if (drone.y < 0.06 || drone.y > 0.94) {
      drone.vy = -drone.vy
      drone.y = Math.min(0.94, Math.max(0.06, drone.y))
    }
  }
  relabel(drones)
}

/** Keyboard labels 1..9 follow screen order (left to right) so they are easy to scan. */
function relabel(drones: Drone[]): void {
  const active = drones.filter((d) => d.alive).sort((a, b) => a.x - b.x)
  drones.forEach((d) => (d.label = 0))
  active.slice(0, 9).forEach((d, index) => (d.label = index + 1))
}

export function activeCount(drones: Drone[]): number {
  return drones.filter((d) => d.alive).length
}

export function remaining(drones: Drone[]): number {
  return drones.filter((d) => !d.hit).length
}

/** Marks the drone hit if it is alive; returns whether the shot connected. */
export function shoot(drones: Drone[], droneId: number): boolean {
  const drone = drones.find((d) => d.id === droneId)
  if (!drone || !drone.alive) return false
  drone.alive = false
  drone.hit = true
  return true
}

export function droneByLabel(drones: Drone[], label: number): Drone | undefined {
  return drones.find((d) => d.alive && d.label === label)
}

/** Client-side preview of the workflow's scoring (the workflow's verdict is authoritative). */
export function previewScore(spec: SkirmishSpecView, tally: SkirmishTally): number {
  const accuracy = spec.targets === 0 ? 0 : tally.hits / spec.targets
  const precision = tally.shotsFired === 0 ? 0 : tally.hits / tally.shotsFired
  return Math.min(1, 0.7 * accuracy + 0.3 * precision)
}
