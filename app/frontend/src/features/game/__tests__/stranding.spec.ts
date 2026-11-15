import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { gamesApi } from '@/api/games-api'
import { http } from '@/api/http'
import type { RouteOptionView } from '@/api/types'
import EmergencyFuelOffer from '../emergency-fuel-offer.vue'
import RoutePicker from '../route-picker.vue'

const safe: RouteOptionView = {
  nodeId: 'o1',
  name: 'Rustwater Depot',
  kind: 'Outpost',
  days: 2,
  fuelCost: 12,
  rationsCost: 10,
  affordable: true,
  warning: null,
}

const risky: RouteOptionView = {
  ...safe,
  nodeId: 's1',
  name: 'New Hope Siding',
  kind: 'Settlement',
  warning: "No fuel depot at New Hope Siding: you'd arrive with 1 fuel, and the shortest route on needs 18.",
}

describe('RoutePicker warnings', () => {
  it('shows the warning only on the route that would strand the ship, labelled for screen readers', () => {
    const wrapper = mount(RoutePicker, { props: { routes: [safe, risky], busy: false, hasRepairInProgress: false, selected: null } })
    const items = wrapper.findAll('li')

    expect(items[0]!.text()).not.toContain('Warning')
    expect(items[1]!.text()).toContain("Warning: No fuel depot at New Hope Siding: you'd arrive with 1 fuel")
  })
})

describe('EmergencyFuelOffer', () => {
  it('spells out the cost and wait, and emits hail when clicked', async () => {
    const wrapper = mount(EmergencyFuelOffer, { props: { offer: { fuel: 17, price: 62, waitDays: 2 }, scrip: 70, busy: false } })

    expect(wrapper.text()).toContain('2 days adrift')
    expect(wrapper.text()).toContain('17 fuel for 62 scrip')
    await wrapper.find('button').trigger('click')

    expect(wrapper.emitted('hail')).toHaveLength(1)
  })
})

describe('commands without parameters', () => {
  it('posts hail to /hail with no body and the command id header', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ status: 200, data: {} })

    await gamesApi.command('g1', { kind: 'hail' }, 'c1')

    expect(post).toHaveBeenCalledWith('/api/games/g1/hail', undefined, { headers: { 'X-Command-Id': 'c1' } })
    post.mockRestore()
  })
})
