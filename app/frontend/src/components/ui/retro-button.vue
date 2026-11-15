<script setup lang="ts">
/** Console-style button. `busy` keeps the label width stable and announces progress to screen readers. */
type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'

interface Props {
  variant?: Variant
  type?: 'button' | 'submit'
  disabled?: boolean
  busy?: boolean
}

const { variant = 'primary', type = 'button', disabled = false, busy = false } = defineProps<Props>()

const variantClasses: Record<Variant, string> = {
  primary: 'border-phosphor bg-phosphor text-void hover:bg-phosphor-dim',
  secondary: 'border-signal text-signal hover:bg-signal/10',
  danger: 'border-rust text-rust hover:bg-rust/10',
  ghost: 'border-transparent text-muted hover:text-dust',
}
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || busy"
    :aria-busy="busy || undefined"
    class="inline-flex min-h-11 items-center justify-center gap-2 rounded-sm border-2 px-4 py-1.5 font-display text-xl tracking-wider uppercase transition-colors disabled:cursor-not-allowed disabled:opacity-50"
    :class="variantClasses[variant]"
  >
    <span v-if="busy" class="animate-blink" aria-hidden="true">▮</span>
    <slot />
  </button>
</template>
