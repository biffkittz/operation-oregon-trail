# CLAUDE.md: frontend (Vue 3 SPA)

Vue 3.5 + TypeScript 6 (strict) + Vite 8 + Tailwind CSS v4 + Pinia + Vue Router 5 + axios + `@microsoft/signalr`.
Built into `dist/`, then copied into the backend's `wwwroot` and served by ASP.NET (same origin as the API).

## Commands

```bash
npm ci
npm run dev          # http://localhost:5173, proxies /api, /hubs, /healthz to the backend on :5069
npm run build        # vue-tsc type-check + vite build
npm run check        # type-check + eslint + oxlint + vitest (run before committing)
npm run test:unit    # vitest watch mode
npm run test:e2e     # Playwright + axe against a running site (E2E_BASE_URL, E2E_PASSWORD)
```

## Structure

| Path | Contents |
|---|---|
| `src/api/` | `http.ts` (axios instance, `ApiError`, 401 hook), typed API modules, `types.ts` (mirrors the C# DTOs; keep in sync with `shared/Oregon.Contracts` and `app/backend/Games`) |
| `src/stores/` | Pinia **setup** stores: `auth`, `settings`, `saves`, `game`, `notifications` |
| `src/composables/` | `useCountdown`, `useTypewriter`, `useGameHub` (SignalR), `useSkirmish`, `useAsyncAction`, `usePrefersReducedMotion` |
| `src/lib/` | Pure helpers: `prng` (seeded), `skirmish` (minigame simulation), `format`, `game-labels` |
| `src/components/ui/` | Presentational building blocks (button, meter, fields, option cards, dialog, toasts, countdown) |
| `src/components/layout/` | App header |
| `src/features/<feature>/` | Views and feature components: `auth`, `saves`, `settings`, `game` |
| `src/assets/main.css` | Tailwind import, `@theme` tokens (colors, fonts), CRT overlay |
| `e2e/` | Playwright smoke tests |

`features/game/game-view.vue` is the only container on the game screen (store + SignalR); its children take props
and emit typed `GameCommand`s.

## Rules

* `<script setup lang="ts">` only; `defineProps<Props>()` (with reactive destructure defaults),
  `defineEmits<{...}>()`, `defineModel()` for two-way binding, generics (`generic="T extends string"`) where
  useful. No Options API.
* File names are kebab-case (`save-slot-card.vue`); components are used in PascalCase. Don't name a prop `slot`
  (it collides with Vue's deprecated `slot` attribute).
* **CSP:** production allows only same-origin scripts and styles. No inline `<script>`, no `style="..."`
  attributes in templates (they become inline styles), no CDNs. Dynamic styles via `:style` bindings are fine
  (applied through the CSSOM). Fonts are bundled from `@fontsource`.
* **No `v-html`.** All LLM text is rendered as text.
* Data flow: the game store applies snapshots by `version` (older ones are ignored); commands send a fresh
  `crypto.randomUUID()` as `X-Command-Id`; a 202 keeps the UI busy until a newer version arrives (with a refetch
  fallback). Deletes are optimistic with rollback.
* Accessibility is part of done: labels, `aria-*`, focus management on navigation and dialogs (native
  `<dialog>`), live regions (toasts, countdown milestones, the log), keyboard play for the skirmish, honoring
  `prefers-reduced-motion` (typewriter instant, drones still, flicker off), WCAG AA contrast from the theme tokens.
  The E2E suite runs axe on every screen it visits.
* Styling: Tailwind utilities with the theme tokens (`bg-void`, `text-phosphor`, `text-signal`, `font-display`,
  ...); shared component classes (`panel`, `field-input`) live in `main.css`.

## Tests

Vitest + Vue Test Utils (`src/**/__tests__`), with typed mocks (`vi.fn<Signature>()`, which oxlint requires).
Composables are tested inside `effectScope()`; timers and animation frames use fake timers. When mounting with a
router, `await router.push(...)` before mounting to avoid racing the router's initial navigation.
