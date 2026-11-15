import process from 'node:process'
import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests run against the full local stack (backend + worker + Postgres + Temporal, fake LLM).
 * Start it first with `./buildAndRunSite.sh`, then run `npm run test:e2e`.
 * Override the target with E2E_BASE_URL (e.g. the Vite dev server at http://localhost:5173).
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60 * 1000,
  expect: { timeout: 10_000 },
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? 'list' : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5069',
    trace: 'retain-on-failure',
    headless: true,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
