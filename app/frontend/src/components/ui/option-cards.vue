<script setup lang="ts" generic="T extends string">
/**
 * Generic radio group rendered as selectable cards (tone, difficulty, text speed). Native radios keep
 * keyboard behavior (arrow keys) and screen-reader semantics for free.
 */
import { useId } from 'vue'

export interface CardOption<V extends string> {
  value: V
  label: string
  description?: string
}

interface Props {
  legend: string
  options: ReadonlyArray<CardOption<T>>
  columns?: 2 | 3 | 4
}

const { legend, options, columns = 2 } = defineProps<Props>()
const model = defineModel<T>({ required: true })

const name = useId()
const gridClass: Record<number, string> = { 2: 'sm:grid-cols-2', 3: 'sm:grid-cols-3', 4: 'sm:grid-cols-2 lg:grid-cols-4' }
</script>

<template>
  <fieldset>
    <legend class="field-label">{{ legend }}</legend>
    <div class="grid grid-cols-1 gap-2" :class="gridClass[columns]">
      <label
        v-for="option in options"
        :key="option.value"
        class="flex cursor-pointer flex-col rounded-sm border-2 px-3 py-2 transition-colors has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-signal"
        :class="model === option.value ? 'border-phosphor bg-phosphor/10' : 'border-edge hover:border-signal-dim'"
      >
        <span class="flex items-center gap-2">
          <input v-model="model" class="sr-only" type="radio" :name="name" :value="option.value" />
          <span class="font-display text-xl text-phosphor" aria-hidden="true">{{ model === option.value ? '◉' : '○' }}</span>
          <span class="font-display text-xl tracking-wide text-dust">{{ option.label }}</span>
        </span>
        <span v-if="option.description" class="pl-7 text-sm text-muted">{{ option.description }}</span>
      </label>
    </div>
  </fieldset>
</template>
