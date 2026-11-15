<script setup lang="ts">
/** The airlock: a single shared password opens the site. */
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import RetroButton from '@/components/ui/retro-button.vue'
import TextField from '@/components/ui/text-field.vue'
import { useAsyncAction } from '@/composables/useAsyncAction'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const password = ref('')
const submitted = ref(false)
const { run: signIn, pending, error } = useAsyncAction((value: string) => auth.login(value))

const validationError = computed(() => (submitted.value && password.value.length === 0 ? 'Enter the password.' : null))
const serverError = computed(() => {
  if (!error.value) return null
  if (error.value.isRateLimited) return 'Too many attempts. The airlock is cycling; wait a minute.'
  if (error.value.isUnauthorized) return 'Wrong password. The airlock stays shut.'
  return error.value.detail
})

async function submit() {
  submitted.value = true
  if (validationError.value) return
  await signIn(password.value)
  if (!error.value) {
    // Only same-site paths are honored, so a crafted ?redirect= can't bounce the player elsewhere.
    const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/') && !route.query.redirect.startsWith('//')
      ? route.query.redirect
      : '/'
    await router.replace(redirect)
  }
}
</script>

<template>
  <section class="mx-auto mt-[12vh] max-w-lg" aria-labelledby="login-title">
    <div class="panel border-2 px-8 py-10">
      <p class="font-display text-xl tracking-[0.4em] text-signal uppercase">Independence Ring · Dock 7</p>
      <h1 id="login-title" class="glow mt-2 font-display text-6xl tracking-[0.2em]">STARWAGON</h1>
      <p class="mt-2 text-dust">Two thousand light-years of bad road between here and Willamette Landing. Speak the word and climb aboard.</p>

      <form class="mt-8 space-y-5" novalidate @submit.prevent="submit">
        <TextField
          v-model="password"
          label="Password"
          type="password"
          autocomplete="current-password"
          :maxlength="256"
          :error="validationError"
          autofocus
          required
        />
        <p v-if="serverError" role="alert" class="rounded-sm border border-rust/60 bg-rust/10 px-3 py-2 text-rust">{{ serverError }}</p>
        <RetroButton type="submit" class="w-full" :busy="pending">Board the wagon</RetroButton>
      </form>
    </div>
  </section>
</template>
