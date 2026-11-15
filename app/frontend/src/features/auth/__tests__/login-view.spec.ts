import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import { authApi } from '@/api/auth-api'
import { ApiError } from '@/api/http'
import LoginView from '../login-view.vue'

vi.mock('@/api/auth-api', () => ({
  authApi: {
    login: vi.fn<(password: string) => Promise<void>>(),
    logout: vi.fn<() => Promise<void>>(),
    session: vi.fn<() => Promise<boolean>>(),
  },
}))

// Navigate before mounting: installing the router on an app at START_LOCATION would trigger its own
// initial navigation and race with ours.
async function setup(initialPath = '/login') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', component: LoginView },
      { path: '/', component: { template: '<p>hangar</p>' } },
      { path: '/game/:id', component: { template: '<p>game</p>' } },
    ],
  })
  await router.push(initialPath)
  await router.isReady()
  const wrapper = mount(LoginView, { global: { plugins: [router] }, attachTo: document.body })
  return { wrapper, router }
}

describe('LoginView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(authApi.login).mockReset()
  })

  it('requires a password before calling the server', async () => {
    const { wrapper } = await setup()
    await wrapper.find('form').trigger('submit')

    expect(authApi.login).not.toHaveBeenCalled()
    const input = wrapper.find('input[type="password"]')
    expect(input.attributes('aria-invalid')).toBe('true')
    expect(wrapper.text()).toContain('Enter the password.')
  })

  it('explains a wrong password in an alert', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new ApiError(401, 'Wrong password', 'nope'))
    const { wrapper } = await setup()
    await wrapper.find('input[type="password"]').setValue('guess')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toContain('Wrong password')
  })

  it('returns to the requested page after signing in, but never off-site', async () => {
    vi.mocked(authApi.login).mockResolvedValue()
    const { wrapper, router } = await setup('/login?redirect=/game/42')
    await wrapper.find('input[type="password"]').setValue('secret')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/game/42')

    const second = await setup('/login?redirect=//evil.example')
    await second.wrapper.find('input[type="password"]').setValue('secret')
    await second.wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(second.router.currentRoute.value.fullPath).toBe('/')
  })
})
