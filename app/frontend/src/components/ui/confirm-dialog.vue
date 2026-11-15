<script setup lang="ts">
/**
 * Modal confirmation using the native <dialog> element: showModal() gives us focus trapping, Escape to close
 * and an inert background without a library. Teleported to <body> so stacking contexts never clip it.
 */
import { nextTick, useId, useTemplateRef, watch } from 'vue'
import RetroButton from './retro-button.vue'

interface Props {
  title: string
  confirmLabel?: string
  danger?: boolean
}

const { title, confirmLabel = 'Confirm', danger = false } = defineProps<Props>()
const open = defineModel<boolean>('open', { required: true })
const emit = defineEmits<{ confirm: [] }>()

const dialog = useTemplateRef<HTMLDialogElement>('dialog')
const titleId = useId()

watch(open, async (isOpen) => {
  await nextTick()
  if (isOpen && !dialog.value?.open) dialog.value?.showModal()
  if (!isOpen && dialog.value?.open) dialog.value.close()
})

function confirm() {
  emit('confirm')
  open.value = false
}
</script>

<template>
  <Teleport to="body">
    <dialog
      ref="dialog"
      :aria-labelledby="titleId"
      class="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-md border-2 border-edge bg-panel p-6 text-dust backdrop:bg-void/80"
      @close="open = false"
    >
      <h2 :id="titleId" class="font-display text-3xl">{{ title }}</h2>
      <div class="mt-3 text-dust">
        <slot />
      </div>
      <div class="mt-6 flex justify-end gap-3">
        <RetroButton variant="ghost" @click="open = false">Cancel</RetroButton>
        <RetroButton :variant="danger ? 'danger' : 'primary'" @click="confirm">{{ confirmLabel }}</RetroButton>
      </div>
    </dialog>
  </Teleport>
</template>
