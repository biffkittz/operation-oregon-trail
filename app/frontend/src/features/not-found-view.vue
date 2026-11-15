<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <section class="mx-auto mt-[15vh] max-w-lg text-center" aria-labelledby="lost-title">
    <h1 id="lost-title" class="glow font-display text-6xl">Lost in the Black</h1>
    <p class="mt-4 text-dust">Your navigator swears this sector was on the map. It was not.</p>
    <RouterLink :to="{ name: 'hangar' }" class="mt-8 inline-block font-display text-2xl text-signal underline underline-offset-4">
      Plot a course home
    </RouterLink>
  </section>
</template>
