<script setup lang="ts">
/** Top bar: title, navigation and sign-out. */
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

async function signOut() {
  await auth.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <header class="border-b border-edge bg-panel/80 backdrop-blur">
    <div class="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-2">
      <RouterLink :to="{ name: 'hangar' }" class="group flex items-baseline gap-3">
        <span class="glow font-display text-4xl tracking-[0.25em] text-phosphor">STARWAGON</span>
        <span class="hidden text-sm text-muted sm:inline">the long haul to the Oregon Reach</span>
      </RouterLink>
      <nav aria-label="Main">
        <ul class="flex items-center gap-1 font-display text-xl tracking-wider uppercase">
          <li>
            <RouterLink :to="{ name: 'hangar' }" class="rounded-sm px-3 py-1 text-signal hover:bg-signal/10" active-class="bg-signal/15">Hangar</RouterLink>
          </li>
          <li>
            <RouterLink :to="{ name: 'settings' }" class="rounded-sm px-3 py-1 text-signal hover:bg-signal/10" active-class="bg-signal/15">Settings</RouterLink>
          </li>
          <li>
            <button type="button" class="rounded-sm px-3 py-1 text-muted uppercase hover:text-rust" @click="signOut">Log out</button>
          </li>
        </ul>
      </nav>
    </div>
  </header>
</template>
