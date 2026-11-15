<script setup lang="ts">
/** Labeled text input with accessible error and hint wiring. */
import { computed, useId } from 'vue'

interface Props {
  label: string
  type?: 'text' | 'password'
  autocomplete?: string
  maxlength?: number
  required?: boolean
  error?: string | null
  hint?: string
  autofocus?: boolean
}

const { label, type = 'text', autocomplete, maxlength, required = false, error = null, hint, autofocus = false } = defineProps<Props>()
const model = defineModel<string>({ required: true })
// 'blur' does not bubble, so it is re-emitted for parents that validate on blur.
const emit = defineEmits<{ blur: [] }>()

const id = useId()
const describedBy = computed(() => [error ? `${id}-error` : null, hint ? `${id}-hint` : null].filter(Boolean).join(' ') || undefined)
</script>

<template>
  <div>
    <label :for="id" class="field-label">{{ label }}<span v-if="required" class="text-rust" aria-hidden="true"> *</span></label>
    <input
      :id="id"
      v-model="model"
      class="field-input"
      :type="type"
      :autocomplete="autocomplete"
      :maxlength="maxlength"
      :required="required"
      :autofocus="autofocus"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="describedBy"
      @blur="emit('blur')"
    />
    <p v-if="hint" :id="`${id}-hint`" class="mt-1 text-sm text-muted">{{ hint }}</p>
    <p v-if="error" :id="`${id}-error`" class="field-error">{{ error }}</p>
  </div>
</template>
