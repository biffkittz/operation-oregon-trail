// Fonts are bundled (not loaded from a CDN) so the strict Content-Security-Policy can stay "self"-only.
import '@fontsource/vt323'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/600.css'
import './assets/main.css'

import { createPinia } from 'pinia'
import { createApp } from 'vue'

import App from './App.vue'
import { onUnauthorized } from './api/http'
import router from './router'
import { useAuthStore } from './stores/auth'
import { useNotificationsStore } from './stores/notifications'

const app = createApp(App)
app.use(createPinia())
app.use(router)

// Last-chance handler for errors that escape components; the player gets a message instead of a dead screen.
app.config.errorHandler = (error, _instance, info) => {
  console.error('[starwagon] unhandled error', info, error)
  useNotificationsStore().push('error', 'Something went wrong on the bridge. Please try again.')
}

// An expired session anywhere sends the player back to the airlock, then returns them where they were.
onUnauthorized(() => {
  useAuthStore().markSignedOut()
  const current = router.currentRoute.value
  if (!current.meta.public) void router.push({ name: 'login', query: { redirect: current.fullPath } })
})

app.mount('#app')
