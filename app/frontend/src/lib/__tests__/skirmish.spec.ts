import { describe, expect, it } from 'vitest'
import type { SkirmishSpecView } from '@/api/types'
import { activeCount, createDrones, droneByLabel, MaxConcurrentDrones, previewScore, remaining, shoot, step } from '../skirmish'

const spec: SkirmishSpecView = { seed: 99, targets: 12, durationSeconds: 20, speedFactor: 1, targetSizePx: 48 }

describe('skirmish simulation', () => {
  it('creates the same wave for the same seed', () => {
    expect(createDrones(spec)).toEqual(createDrones(spec))
  })

  it('never shows more than the concurrent limit and keeps drones inside the arena', () => {
    const drones = createDrones(spec)
    let previous = 0
    for (let now = 0; now <= 20_000; now += 50) {
      step(drones, previous, now)
      previous = now
      expect(activeCount(drones)).toBeLessThanOrEqual(MaxConcurrentDrones)
      for (const drone of drones.filter((d) => d.alive)) {
        expect(drone.x).toBeGreaterThanOrEqual(0.04)
        expect(drone.x).toBeLessThanOrEqual(0.96)
      }
    }
  })

  it('labels visible drones 1..n from left to right for keyboard play', () => {
    const drones = createDrones(spec)
    step(drones, 0, 12_000)
    const labelled = drones.filter((d) => d.alive && d.label > 0).sort((a, b) => a.label - b.label)
    expect(labelled.map((d) => d.label)).toEqual(labelled.map((_, i) => i + 1))
    expect([...labelled].sort((a, b) => a.x - b.x)).toEqual(labelled)
  })

  it('counts a hit only once', () => {
    const drones = createDrones(spec)
    step(drones, 0, 1_000)
    const target = droneByLabel(drones, 1)!
    expect(shoot(drones, target.id)).toBe(true)
    expect(shoot(drones, target.id)).toBe(false)
    expect(remaining(drones)).toBe(spec.targets - 1)
  })

  it('keeps drones still when reduced motion is requested', () => {
    const drones = createDrones(spec, { still: true })
    step(drones, 0, 500)
    const before = drones.filter((d) => d.alive).map((d) => [d.x, d.y])
    step(drones, 500, 5_000)
    expect(drones.filter((d) => d.alive).slice(0, before.length).map((d) => [d.x, d.y])).toEqual(before)
  })

  it('previews the server scoring formula', () => {
    expect(previewScore(spec, { hits: 12, shotsFired: 12, elapsedMs: 10_000 })).toBe(1)
    expect(previewScore(spec, { hits: 0, shotsFired: 30, elapsedMs: 10_000 })).toBe(0)
  })
})
