<script setup lang="ts">
/** Accessible on/off switch (role="switch"). */
interface Props {
  label: string
  description?: string
}

defineProps<Props>()
const model = defineModel<boolean>({ required: true })
</script>

<template>
  <div class="flex items-center justify-between gap-4">
    <div>
      <p class="font-display text-xl tracking-wide text-dust">{{ label }}</p>
      <p v-if="description" class="text-sm text-muted">{{ description }}</p>
    </div>
    <button
      type="button"
      role="switch"
      :aria-checked="model"
      :aria-label="label"
      class="relative h-7 w-14 shrink-0 rounded-full border-2 transition-colors"
      :class="model ? 'border-signal bg-signal/20' : 'border-edge bg-void'"
      @click="model = !model"
    >
      <span
        class="absolute top-0.5 h-5 w-5 rounded-full transition-all"
        :class="model ? 'left-7 bg-signal' : 'left-0.5 bg-muted'"
        aria-hidden="true"
      />
    </button>
  </div>
</template>
