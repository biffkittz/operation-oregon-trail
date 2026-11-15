import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import type { ChallengePromptView } from '@/api/types'
import ChallengePrompt from '../challenge-prompt.vue'

const challenge: ChallengePromptView = {
  kind: 'Haggle',
  stat: 'Diplomacy',
  title: "The Smith's Price",
  description: 'Old Marta wants 60 scrip.',
  npcName: 'Old Marta',
  npcLine: 'Price is the price.',
  stakes: 'Success: a discount. Failure: full price.',
  deadline: null,
  maxResponseLength: 600,
}

describe('ChallengePrompt', () => {
  it('only submits a non-empty, trimmed response', async () => {
    const wrapper = mount(ChallengePrompt, { props: { challenge, busy: false, serverOffsetMs: 0 } })
    const button = wrapper.find('button[type="submit"]')
    expect(button.attributes('disabled')).toBeDefined()

    await wrapper.find('textarea').setValue('  I offer to carry her letters.  ')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([['I offer to carry her letters.']])
  })

  it('shows the counterpart line and the stakes', () => {
    const wrapper = mount(ChallengePrompt, { props: { challenge, busy: false, serverOffsetMs: 0 } })
    expect(wrapper.text()).toContain('Old Marta')
    expect(wrapper.text()).toContain('Success: a discount')
  })

  it('locks the form once the deadline has passed', () => {
    const expired = { ...challenge, deadline: new Date(Date.now() - 1_000).toISOString() }
    const wrapper = mount(ChallengePrompt, { props: { challenge: expired, busy: false, serverOffsetMs: 0 } })
    expect(wrapper.find('textarea').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain("Time's up")
  })
})
