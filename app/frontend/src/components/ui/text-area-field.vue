<script setup lang="ts">
/** Labeled textarea with a live character counter (announced politely near the limit). */
import { computed, useId } from 'vue'

interface Props {
  label: string
  maxlength: number
  rows?: number
  placeholder?: string
  error?: string | null
  disabled?: boolean
  hideLabel?: boolean
}

const { label, maxlength, rows = 4, placeholder, error = null, disabled = false, hideLabel = false } = defineProps<Props>()
const model = defineModel<string>({ required: true })
// 'blur' does not bubble, so it is re-emitted for parents that validate on blur.
const emit = defineEmits<{ blur: [] }>()

const id = useId()
const remaining = computed(() => maxlength - model.value.length)
const nearLimit = computed(() => remaining.value <= Math.max(20, Math.round(maxlength * 0.1)))
</script>

<template>
  <div>
    <label :for="id" class="field-label" :class="{ 'sr-only': hideLabel }">{{ label }}</label>
    <textarea
      :id="id"
      v-model="model"
      class="field-input resize-y leading-relaxed"
      :rows="rows"
      :maxlength="maxlength"
      :placeholder="placeholder"
      :disabled="disabled"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="`${id}-count${error ? ` ${id}-error` : ''}`"
      @blur="emit('blur')"
    />
    <div class="mt-1 flex justify-between gap-4 text-sm">
      <p v-if="error" :id="`${id}-error`" class="field-error mt-0">{{ error }}</p>
      <p :id="`${id}-count`" class="ml-auto tabular-nums" :class="nearLimit ? 'text-phosphor' : 'text-muted'" :aria-live="nearLimit ? 'polite' : 'off'">
        {{ remaining }} characters left
      </p>
    </div>
  </div>
</template>
