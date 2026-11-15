<script setup lang="ts">
/**
 * The trail map as SVG, drawn top (Independence Ring) to bottom (Willamette Landing) so it fits a narrow
 * sidebar and phones without horizontal scrolling. Each band is one leg of the journey. Reachable nodes are
 * buttons (keyboard accessible); the route picker offers the same choices as a plain list for screen readers.
 */
import { computed } from 'vue'
import type { MapNodeView, MapView } from '@/api/types'
import { nodeKindInfo } from '@/lib/game-labels'

interface Props {
  map: MapView
  selectedNodeId?: string | null
}

const { map, selectedNodeId = null } = defineProps<Props>()
const emit = defineEmits<{ select: [nodeId: string] }>()

const Width = 320
const BandHeight = 78

const height = computed(() => map.columns * BandHeight)
const positions = computed(() =>
  new Map(map.nodes.map((n) => [n.id, { x: ((n.row + 0.5) / n.rowsInColumn) * Width, y: n.column * BandHeight + BandHeight / 2 - 8 }])),
)
const nodesById = computed(() => new Map(map.nodes.map((n) => [n.id, n])))
const selected = computed(() => (selectedNodeId ? nodesById.value.get(selectedNodeId) : undefined))
const current = computed(() => nodesById.value.get(map.currentNodeId))

function edgeClass(from: string, to: string): string {
  const a = nodesById.value.get(from)
  const b = nodesById.value.get(to)
  if (from === map.currentNodeId && b?.reachable) return 'stroke-signal'
  return a?.visited && b?.visited ? 'stroke-phosphor-dim' : 'stroke-edge'
}

function nodeClass(node: MapNodeView): string {
  if (node.id === map.currentNodeId) return 'fill-phosphor stroke-phosphor'
  if (node.id === selectedNodeId) return 'fill-signal/30 stroke-signal'
  if (node.reachable) return 'fill-void stroke-signal'
  return node.visited ? 'fill-phosphor-dim/40 stroke-phosphor-dim' : 'fill-void stroke-edge'
}

function select(node: MapNodeView) {
  if (node.reachable) emit('select', node.id)
}
</script>

<template>
  <section class="panel" aria-labelledby="map-title">
    <h2 id="map-title" class="panel-title">Trail map</h2>
    <div class="mx-auto max-w-sm">
      <svg
        :viewBox="`0 0 ${Width} ${height}`"
        class="h-auto w-full"
        role="group"
        :aria-label="`Route map. You are at ${current?.name ?? 'the start'}.`"
      >
        <line
          v-for="edge in map.edges"
          :key="`${edge.from}-${edge.to}`"
          :x1="positions.get(edge.from)?.x"
          :y1="positions.get(edge.from)?.y"
          :x2="positions.get(edge.to)?.x"
          :y2="positions.get(edge.to)?.y"
          :class="edgeClass(edge.from, edge.to)"
          stroke-width="2"
          :stroke-dasharray="edge.from === map.currentNodeId ? '6 4' : undefined"
        />
        <g
          v-for="node in map.nodes"
          :key="node.id"
          :transform="`translate(${positions.get(node.id)?.x}, ${positions.get(node.id)?.y})`"
          :role="node.reachable ? 'button' : undefined"
          :tabindex="node.reachable ? 0 : undefined"
          :aria-label="node.reachable ? `Select ${node.name}, ${nodeKindInfo[node.kind].label}` : undefined"
          :aria-pressed="node.reachable ? node.id === selectedNodeId : undefined"
          :class="node.reachable ? 'cursor-pointer focus:outline-none [&:focus-visible>circle]:stroke-[5]' : ''"
          @click="select(node)"
          @keydown.enter.prevent="select(node)"
          @keydown.space.prevent="select(node)"
        >
          <title>{{ node.name }} ({{ nodeKindInfo[node.kind].label }}): {{ node.description }}</title>
          <circle r="17" :class="nodeClass(node)" stroke-width="2.5" />
          <text text-anchor="middle" dy="6" class="font-display text-[18px]" :class="node.id === map.currentNodeId ? 'fill-void' : 'fill-dust'">
            {{ nodeKindInfo[node.kind].glyph }}
          </text>
          <text
            v-if="node.reachable || node.id === map.currentNodeId || node.kind === 'Destination'"
            text-anchor="middle"
            y="33"
            class="font-mono text-[11px]"
            :class="node.id === map.currentNodeId ? 'fill-phosphor' : node.reachable ? 'fill-signal' : 'fill-muted'"
          >
            {{ node.name.length > 22 ? `${node.name.slice(0, 21)}…` : node.name }}
          </text>
        </g>
      </svg>
    </div>
    <p v-if="selected" class="mt-2 text-sm">
      <span class="font-display text-lg text-signal">{{ selected.name }}</span>
      <span class="text-muted"> · {{ nodeKindInfo[selected.kind].label }}</span><br />
      <span class="text-dust">{{ selected.description }}</span>
    </p>
    <p v-else-if="current" class="mt-2 text-sm text-dust">
      Docked at <span class="text-phosphor">{{ current.name }}</span>: {{ current.description }}
    </p>
  </section>
</template>
