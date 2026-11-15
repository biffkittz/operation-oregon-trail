<script setup lang="ts">
/** Blinking terminal cursor with an accessible status message. */
interface Props {
  label?: string
}

const { label = 'Loading' } = defineProps<Props>()
</script>

<template>
  <p role="status" class="font-display text-2xl text-phosphor">
    <span>{{ label }}</span>
    <span class="ml-1 animate-blink" aria-hidden="true">▮</span>
  </p>
</template>
