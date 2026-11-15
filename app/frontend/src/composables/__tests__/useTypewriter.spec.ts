import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { effectScope, nextTick, ref } from 'vue'
import { useTypewriter } from '../useTypewriter'

describe('useTypewriter', () => {
  beforeEach(() => vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] }))
  afterEach(() => vi.useRealTimers())

  it('reveals text over time and can be skipped', async () => {
    const scope = effectScope()
    const typewriter = scope.run(() => useTypewriter(ref('Hello trail'), ref(10)))!
    expect(typewriter.done.value).toBe(false)

    vi.advanceTimersByTime(50)
    expect(typewriter.shown.value.length).toBeGreaterThan(0)
    expect(typewriter.shown.value.length).toBeLessThan('Hello trail'.length)

    typewriter.finish()
    expect(typewriter.shown.value).toBe('Hello trail')
    expect(typewriter.done.value).toBe(true)
    scope.stop()
  })

  it('shows text instantly at speed 0 (Instant setting or reduced motion)', async () => {
    const scope = effectScope()
    const text = ref('First')
    const typewriter = scope.run(() => useTypewriter(text, ref(0)))!
    expect(typewriter.shown.value).toBe('First')

    text.value = 'Second'
    await nextTick()
    expect(typewriter.shown.value).toBe('Second')
    scope.stop()
  })
})
