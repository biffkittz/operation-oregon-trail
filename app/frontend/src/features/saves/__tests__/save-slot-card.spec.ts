import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import type { SaveSlot } from '@/api/types'
import SaveSlotCard from '../save-slot-card.vue'

const game: NonNullable<SaveSlot['game']> = {
  gameId: 'g1',
  title: 'Dust and Bound Pages',
  captainName: 'Ines',
  shipName: 'Long Shelf',
  tone: 'Hopeful',
  difficulty: 'Normal',
  status: 'Victory',
  phase: 'Finished',
  day: 14,
  hull: 62,
  crew: 6,
  locationName: 'Willamette Landing',
  score: 1840,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
}

describe('SaveSlotCard', () => {
  it('offers a new voyage for an empty berth', async () => {
    const wrapper = mount(SaveSlotCard, { props: { entry: { slotNumber: 3, game: null } } })
    expect(wrapper.text()).toContain('Empty berth')
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('create')).toEqual([[3]])
  })

  it('summarizes a finished voyage and labels the scuttle button accessibly', async () => {
    const wrapper = mount(SaveSlotCard, { props: { entry: { slotNumber: 1, game } } })
    expect(wrapper.text()).toContain('Dust and Bound Pages')
    expect(wrapper.text()).toContain('1840')
    expect(wrapper.text()).toContain('Review')
    const scuttle = wrapper.find('button[aria-label="Scuttle Dust and Bound Pages"]')
    await scuttle.trigger('click')
    expect(wrapper.emitted('scuttle')).toEqual([['g1', 'Dust and Bound Pages']])
  })
})
