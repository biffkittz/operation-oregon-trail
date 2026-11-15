<script setup lang="ts">
/**
 * The captain's log. The newest entry is "typed" in; screen readers get the complete text immediately via a
 * visually hidden copy, while the animated copy is aria-hidden.
 */
import { computed, nextTick, onMounted, ref, useTemplateRef, watch } from 'vue'
import type { JournalEntryKind, JournalEntryView } from '@/api/types'
import { useTypewriter } from '@/composables/useTypewriter'

interface Props {
  entries: JournalEntryView[]
  hasOlder: boolean
  msPerChar: number
}

const { entries, hasOlder, msPerChar } = defineProps<Props>()
const emit = defineEmits<{ loadOlder: [] }>()

const scroller = useTemplateRef<HTMLDivElement>('scroller')
const stickToBottom = ref(true)

// Only entries that arrive while the log is on screen get the typewriter treatment.
const lastSeenSequence = ref(Number.POSITIVE_INFINITY)
onMounted(() => (lastSeenSequence.value = entries.at(-1)?.sequence ?? 0))

const newest = computed(() => entries.at(-1))
const animateNewest = computed(() => !!newest.value && newest.value.sequence > lastSeenSequence.value)
const { shown, done, finish } = useTypewriter(() => (animateNewest.value ? (newest.value?.text ?? '') : ''), () => msPerChar)

const kindClass: Record<JournalEntryKind, string> = {
  Narration: 'text-dust',
  Player: 'text-signal',
  Npc: 'text-lavender italic',
  Outcome: 'text-phosphor',
  Event: 'text-muted',
  System: 'text-lavender text-sm',
}

function onScroll() {
  const el = scroller.value
  if (el) stickToBottom.value = el.scrollHeight - el.scrollTop - el.clientHeight < 40
}

watch(
  () => [entries.length, shown.value.length],
  async () => {
    await nextTick()
    if (stickToBottom.value && scroller.value) scroller.value.scrollTop = scroller.value.scrollHeight
  },
  { flush: 'post' },
)

onMounted(() => {
  if (scroller.value) scroller.value.scrollTop = scroller.value.scrollHeight
})
</script>

<template>
  <section class="panel flex min-h-0 flex-col" aria-labelledby="log-title">
    <div class="flex items-baseline justify-between">
      <h2 id="log-title" class="panel-title">Captain's log</h2>
      <button v-if="!done" type="button" class="font-display text-lg text-muted hover:text-dust" @click="finish">Skip ▸▸</button>
    </div>
    <div ref="scroller" class="max-h-[46vh] min-h-48 overflow-y-auto pr-2" role="log" aria-label="Captain's log" @scroll.passive="onScroll">
      <button v-if="hasOlder && entries.length > 0" type="button" class="mb-3 font-display text-lg text-signal underline underline-offset-4" @click="emit('loadOlder')">
        Read older entries
      </button>
      <ol class="space-y-3 leading-relaxed">
        <li v-for="entry in entries" :key="entry.sequence" :class="kindClass[entry.kind]">
          <span class="mr-2 font-display text-base text-muted">D{{ entry.day }}</span>
          <template v-if="animateNewest && entry.sequence === newest?.sequence">
            <span class="sr-only">{{ entry.text }}</span>
            <span aria-hidden="true" class="whitespace-pre-line">{{ shown }}<span v-if="!done" class="animate-blink">▮</span></span>
          </template>
          <span v-else class="whitespace-pre-line">
            <template v-if="entry.kind === 'Player'">&gt; </template>{{ entry.text }}
          </span>
        </li>
      </ol>
    </div>
  </section>
</template>
