import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { effectScope, nextTick, ref } from 'vue'
import { useCountdown } from '../useCountdown'

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2291-04-01T12:00:00Z'))
  })
  afterEach(() => vi.useRealTimers())

  it('counts down and escalates urgency', async () => {
    const scope = effectScope()
    const countdown = scope.run(() => useCountdown(ref('2291-04-01T12:01:00Z')))!
    expect(countdown.secondsLeft.value).toBe(60)
    expect(countdown.urgency.value).toBe('calm')

    vi.advanceTimersByTime(35_000)
    await nextTick()
    expect(countdown.urgency.value).toBe('warning')

    vi.advanceTimersByTime(20_000)
    await nextTick()
    expect(countdown.urgency.value).toBe('critical')

    vi.advanceTimersByTime(10_000)
    await nextTick()
    expect(countdown.expired.value).toBe(true)
    expect(countdown.secondsLeft.value).toBe(0)
    scope.stop()
  })

  it('corrects for a skewed browser clock using the server offset', () => {
    const scope = effectScope()
    // The server is 5 seconds ahead of this browser.
    const countdown = scope.run(() => useCountdown(ref('2291-04-01T12:01:00Z'), ref(5_000)))!
    expect(countdown.secondsLeft.value).toBe(55)
    scope.stop()
  })

  it('reports no deadline as null', () => {
    const scope = effectScope()
    const countdown = scope.run(() => useCountdown(ref(null)))!
    expect(countdown.secondsLeft.value).toBeNull()
    expect(countdown.urgency.value).toBe('none')
    scope.stop()
  })
})
