<script setup lang="ts">
/** Renders notifications in a polite live region (errors use role="alert"). */
import { useNotificationsStore } from '@/stores/notifications'

const notifications = useNotificationsStore()
const toneClass = { info: 'border-signal', success: 'border-sage', error: 'border-rust' } as const
</script>

<template>
  <div class="pointer-events-none fixed right-4 bottom-4 z-50 flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2" aria-live="polite">
    <div
      v-for="toast in notifications.toasts"
      :key="toast.id"
      :role="toast.kind === 'error' ? 'alert' : 'status'"
      class="pointer-events-auto flex items-start gap-3 rounded-sm border-l-4 bg-panel-raised px-4 py-3 shadow-lg"
      :class="toneClass[toast.kind]"
    >
      <p class="flex-1 text-sm text-dust">{{ toast.message }}</p>
      <button type="button" class="font-display text-lg text-muted hover:text-dust" aria-label="Dismiss" @click="notifications.dismiss(toast.id)">×</button>
    </div>
  </div>
</template>
