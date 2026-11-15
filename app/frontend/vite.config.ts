import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import vueDevTools from 'vite-plugin-vue-devtools'

// The ASP.NET backend serves the built SPA in production. During `npm run dev`, Vite serves the SPA and
// proxies API, SignalR and health calls to the backend so the browser sees a single origin (cookies just work).
const backend = process.env.OREGON_BACKEND_URL ?? 'http://localhost:5069'

export default defineConfig({
  plugins: [vue(), tailwindcss(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: backend, changeOrigin: false },
      '/hubs': { target: backend, ws: true, changeOrigin: false },
      '/healthz': { target: backend, changeOrigin: false },
    },
  },
  build: {
    // Inline nothing: the production CSP forbids inline scripts and styles.
    assetsInlineLimit: 0,
    sourcemap: false,
  },
})
