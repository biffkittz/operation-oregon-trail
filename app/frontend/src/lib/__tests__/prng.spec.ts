import { describe, expect, it } from 'vitest'
import { mulberry32 } from '../prng'

describe('mulberry32', () => {
  it('is deterministic for a seed', () => {
    const a = mulberry32(42)
    const b = mulberry32(42)
    expect(Array.from({ length: 5 }, a)).toEqual(Array.from({ length: 5 }, b))
  })

  it('produces values in [0, 1)', () => {
    const next = mulberry32(7)
    for (let i = 0; i < 1000; i++) {
      const value = next()
      expect(value).toBeGreaterThanOrEqual(0)
      expect(value).toBeLessThan(1)
    }
  })
})
