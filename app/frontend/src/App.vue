<script setup lang="ts">
import { computed, nextTick, onMounted, useTemplateRef, watch } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import AppHeader from '@/components/layout/app-header.vue'
import LoadingCursor from '@/components/ui/loading-cursor.vue'
import ToastRegion from '@/components/ui/toast-region.vue'
import { useAuthStore } from '@/stores/auth'
import { useSettingsStore } from '@/stores/settings'

const route = useRoute()
const auth = useAuthStore()
const settings = useSettingsStore()
const main = useTemplateRef<HTMLElement>('main')

const bare = computed(() => route.meta.layout === 'bare')

// Display settings (CRT, text speed) load once we know there is a session.
watch(
  () => auth.authenticated,
  (signedIn) => {
    if (signedIn && !settings.loaded) void settings.load().catch(() => undefined)
  },
  { immediate: true },
)

// After client-side navigation, move focus to the main region so screen readers announce the new page.
let firstNavigation = true
watch(
  () => route.fullPath,
  async () => {
    if (firstNavigation) {
      firstNavigation = false
      return
    }
    await nextTick()
    main.value?.focus({ preventScroll: true })
  },
)

onMounted(() => document.documentElement.setAttribute('lang', 'en'))
</script>

<template>
  <div class="min-h-dvh" :class="{ crt: settings.crtEnabled }">
    <a
      href="#main"
      class="sr-only z-50 rounded-sm bg-phosphor px-3 py-2 font-display text-xl text-void focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
    >
      Skip to content
    </a>
    <AppHeader v-if="!bare" />
    <main id="main" ref="main" tabindex="-1" class="mx-auto w-full max-w-7xl px-4 py-6 focus:outline-none">
      <RouterView v-slot="{ Component }">
        <Suspense>
          <component :is="Component" />
          <template #fallback>
            <LoadingCursor label="Loading" />
          </template>
        </Suspense>
      </RouterView>
    </main>
    <ToastRegion />
  </div>
</template>
