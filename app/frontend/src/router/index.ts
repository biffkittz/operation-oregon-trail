import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    /** Page title (suffixed with the game name). */
    title: string
    /** Reachable without a session. */
    public?: boolean
    /** "bare" hides the app header (login screen). */
    layout?: 'bare'
    /** Label for breadcrumbs in the header. */
    breadcrumb?: string
  }
}

// Every view is lazy-loaded so the login screen ships without the game code.
const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/features/auth/login-view.vue'),
    meta: { title: 'Board the Wagon', public: true, layout: 'bare' },
  },
  {
    path: '/',
    name: 'hangar',
    component: () => import('@/features/saves/hangar-view.vue'),
    meta: { title: 'Hangar', breadcrumb: 'Hangar' },
  },
  {
    path: '/new',
    name: 'new-game',
    component: () => import('@/features/saves/new-game-view.vue'),
    meta: { title: 'New Voyage', breadcrumb: 'New voyage' },
  },
  {
    path: '/game/:gameId',
    name: 'game',
    component: () => import('@/features/game/game-view.vue'),
    props: true,
    meta: { title: 'On the Trail', breadcrumb: 'Voyage' },
  },
  {
    path: '/settings',
    name: 'settings',
    component: () => import('@/features/settings/settings-view.vue'),
    meta: { title: 'Settings', breadcrumb: 'Settings' },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/features/not-found-view.vue'),
    meta: { title: 'Lost in the Black', public: true },
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (auth.authenticated === null) await auth.checkSession()
  if (!to.meta.public && !auth.authenticated) {
    return { name: 'login', query: to.fullPath === '/' ? {} : { redirect: to.fullPath } }
  }
  if (to.name === 'login' && auth.authenticated) return { name: 'hangar' }
  return true
})

router.afterEach((to) => {
  document.title = `${to.meta.title} · STARWAGON`
})

export default router
