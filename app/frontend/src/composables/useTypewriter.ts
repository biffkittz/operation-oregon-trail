import { onScopeDispose, ref, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'

export interface Typewriter {
  shown: Ref<string>
  done: Ref<boolean>
  /** Reveal the whole text immediately (click or keypress). */
  finish: () => void
}

/**
 * Retro "teletype" reveal. Purely visual: components render the full text for assistive technology and mark
 * the animated copy aria-hidden, so screen readers never hear a half-typed sentence. A speed of 0 (Instant
 * text, or reduced motion) shows text immediately.
 */
export function useTypewriter(text: MaybeRefOrGetter<string>, msPerChar: MaybeRefOrGetter<number>): Typewriter {
  const shown = ref('')
  const done = ref(true)
  let frame: number | undefined

  const cancel = () => {
    if (frame !== undefined) cancelAnimationFrame(frame)
    frame = undefined
  }

  const finish = () => {
    cancel()
    shown.value = toValue(text)
    done.value = true
  }

  watch(
    () => [toValue(text), toValue(msPerChar)] as const,
    ([value, speed]) => {
      cancel()
      if (!value || speed <= 0) {
        shown.value = value
        done.value = true
        return
      }
      shown.value = ''
      done.value = false
      const start = performance.now()
      const tick = (time: number) => {
        const count = Math.min(value.length, Math.floor((time - start) / speed))
        shown.value = value.slice(0, count)
        if (count >= value.length) {
          done.value = true
          frame = undefined
        } else {
          frame = requestAnimationFrame(tick)
        }
      }
      frame = requestAnimationFrame(tick)
    },
    { immediate: true },
  )
  onScopeDispose(cancel)

  return { shown, done, finish }
}
