import { computed, onScopeDispose, ref, toValue, watch, type ComputedRef, type MaybeRefOrGetter } from 'vue'

export type Urgency = 'none' | 'calm' | 'warning' | 'critical'

export interface Countdown {
  /** Seconds until the deadline (never negative), or null when there is no deadline. */
  secondsLeft: ComputedRef<number | null>
  expired: ComputedRef<boolean>
  urgency: ComputedRef<Urgency>
}

/**
 * Live countdown to a server-issued deadline. `serverOffsetMs` (server clock minus browser clock) corrects for
 * a skewed local clock, so the bar the player sees matches the workflow's timer.
 */
export function useCountdown(
  deadline: MaybeRefOrGetter<string | null | undefined>,
  serverOffsetMs: MaybeRefOrGetter<number> = 0,
): Countdown {
  const now = ref(Date.now())
  let timer: ReturnType<typeof setInterval> | undefined

  const stop = () => {
    if (timer !== undefined) clearInterval(timer)
    timer = undefined
  }

  watch(
    () => toValue(deadline),
    (value) => {
      stop()
      if (value) {
        now.value = Date.now()
        timer = setInterval(() => (now.value = Date.now()), 250)
      }
    },
    { immediate: true },
  )
  onScopeDispose(stop)

  const secondsLeft = computed(() => {
    const value = toValue(deadline)
    if (!value) return null
    const serverNow = now.value + toValue(serverOffsetMs)
    return Math.max(0, (new Date(value).getTime() - serverNow) / 1000)
  })

  const expired = computed(() => secondsLeft.value === 0)
  const urgency = computed<Urgency>(() => {
    const left = secondsLeft.value
    if (left === null) return 'none'
    if (left <= 10) return 'critical'
    return left <= 30 ? 'warning' : 'calm'
  })

  return { secondsLeft, expired, urgency }
}
