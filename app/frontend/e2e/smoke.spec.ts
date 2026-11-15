import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'

/**
 * Smoke tests against the full local stack (see playwright.config.ts). They use the development password and
 * work with either the fake or the real LLM. Each test cleans up the voyage it creates.
 */
const password = process.env.E2E_PASSWORD ?? 'oregon'

async function signIn(page: Page) {
  await page.goto('/login')
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: /board the wagon/i }).click()
  await expect(page.getByRole('heading', { name: 'The Hangar' })).toBeVisible()
}

/** Collects CSP violations reported by the browser (they would silently break styles or scripts). */
function trackCspViolations(page: Page): string[] {
  const violations: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error' && /content security policy/i.test(message.text())) violations.push(message.text())
  })
  return violations
}

async function expectNoAxeViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()
  expect(results.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`)).toEqual([])
}

test('the login screen is accessible and turns away a wrong password', async ({ page }) => {
  const csp = trackCspViolations(page)
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'STARWAGON' })).toBeVisible()
  await expectNoAxeViolations(page)

  await page.getByLabel('Password').fill('definitely-wrong')
  await page.getByRole('button', { name: /board the wagon/i }).click()

  await expect(page.getByRole('alert')).toContainText('Wrong password')
  expect(csp).toEqual([])
})

test('a captain can launch a voyage, read the prologue and reach the route map', async ({ page }) => {
  const csp = trackCspViolations(page)
  await signIn(page)
  await expectNoAxeViolations(page)

  await page.getByRole('button', { name: 'New voyage' }).first().click()
  await expect(page.getByRole('heading', { name: 'Outfit a new wagon' })).toBeVisible()
  await page.getByLabel('Captain').fill('E2E Tester')
  await page.getByLabel('Wagon name').fill('Test Mule')
  await page.getByRole('button', { name: 'Generate with AI' }).click()
  await expect(page.getByLabel('Story seed')).not.toHaveValue('', { timeout: 30_000 })
  await expectNoAxeViolations(page)
  await page.getByRole('button', { name: 'Cast off' }).click()

  await expect(page).toHaveURL(/\/game\//)
  await expect(page.getByRole('heading', { name: 'Prologue' })).toBeVisible({ timeout: 30_000 })
  await expect(page.getByText('Live')).toBeVisible()
  const voyageTitle = (await page.getByRole('heading', { level: 1 }).textContent())?.trim() ?? ''

  // Clicking the text completes the typewriter (a no-op if it already finished); then acknowledge the prologue.
  const prologue = page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Prologue' }) })
  await prologue.locator('p[aria-hidden="true"]').click()
  await prologue.getByRole('button', { name: 'Cast off' }).click()

  const routeList = page.getByRole('group', { name: 'Set a course' })
  await expect(routeList).toBeVisible()
  await expect(page.getByRole('group', { name: /route map/i })).toBeVisible()
  await expectNoAxeViolations(page)

  // Pick the first route by clicking its (visible) label, as a player would, and depart.
  await routeList.locator('label').first().click()
  await expect(routeList.getByRole('radio').first()).toBeChecked()
  await page.getByRole('button', { name: /head for/i }).click()
  await expect(page.getByRole('log', { name: "Captain's log" })).toContainText('burns', { timeout: 30_000 })
  expect(csp).toEqual([])

  // Clean up: scuttle the voyage from the hangar.
  await page.getByRole('link', { name: 'Hangar' }).click()
  await page.getByRole('button', { name: `Scuttle ${voyageTitle}` }).first().click()
  await page.getByRole('dialog').getByRole('button', { name: 'Scuttle' }).click()
  await expect(page.getByRole('status').filter({ hasText: 'was scuttled' })).toBeVisible()
})
