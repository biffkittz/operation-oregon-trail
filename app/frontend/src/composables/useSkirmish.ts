import { computed, onScopeDispose, ref, shallowRef, triggerRef, toValue, type MaybeRefOrGetter } from 'vue'
import type { SkirmishSpecView } from '@/api/types'
import { createDrones, droneByLabel, remaining, shoot, step, type Drone, type SkirmishTally } from '@/lib/skirmish'

/**
 * Drives the click battle with requestAnimationFrame. Drone positions live in a shallowRef that is triggered
 * once per frame: one re-render per frame instead of deep reactivity on every coordinate.
 */
export function useSkirmish(
  spec: MaybeRefOrGetter<SkirmishSpecView>,
  onFinished: (tally: SkirmishTally) => void,
  options: { still?: MaybeRefOrGetter<boolean> } = {},
) {
  const drones = shallowRef<Drone[]>([])
  const running = ref(false)
  const finished = ref(false)
  const elapsedMs = ref(0)
  const hits = ref(0)
  const shotsFired = ref(0)
  let startedAt = 0
  let lastMs = 0
  let frame: number | undefined

  const durationMs = computed(() => toValue(spec).durationSeconds * 1000)
  const secondsLeft = computed(() => Math.max(0, (durationMs.value - elapsedMs.value) / 1000))

  function start() {
    drones.value = createDrones(toValue(spec), { still: toValue(options.still ?? false) })
    hits.value = 0
    shotsFired.value = 0
    elapsedMs.value = 0
    lastMs = 0
    finished.value = false
    running.value = true
    startedAt = performance.now()
    frame = requestAnimationFrame(tick)
  }

  function tick(time: number) {
    const now = time - startedAt
    step(drones.value, lastMs, now)
    lastMs = now
    elapsedMs.value = now
    triggerRef(drones)
    if (now >= durationMs.value || remaining(drones.value) === 0) {
      finish()
    } else {
      frame = requestAnimationFrame(tick)
    }
  }

  function fireAt(droneId: number) {
    if (!running.value) return
    shotsFired.value++
    if (shoot(drones.value, droneId)) hits.value++
    triggerRef(drones)
    if (remaining(drones.value) === 0) finish()
  }

  /** A shot that hit empty space. */
  function miss() {
    if (running.value) shotsFired.value++
  }

  /** Keyboard mode: fire at the drone currently labelled `label` (1-9). */
  function fireLabel(label: number) {
    const drone = droneByLabel(drones.value, label)
    if (drone) fireAt(drone.id)
    else miss()
  }

  function finish() {
    if (frame !== undefined) cancelAnimationFrame(frame)
    frame = undefined
    if (!running.value) return
    running.value = false
    finished.value = true
    onFinished({ hits: hits.value, shotsFired: shotsFired.value, elapsedMs: Math.round(elapsedMs.value) })
  }

  onScopeDispose(() => {
    if (frame !== undefined) cancelAnimationFrame(frame)
  })

  return { drones, running, finished, elapsedMs, secondsLeft, hits, shotsFired, start, fireAt, miss, fireLabel }
}
