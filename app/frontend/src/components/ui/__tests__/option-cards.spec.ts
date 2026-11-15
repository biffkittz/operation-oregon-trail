import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import OptionCards from '../option-cards.vue'

describe('OptionCards', () => {
  it('works as an accessible radio group with v-model', async () => {
    const wrapper = mount(OptionCards, {
      props: {
        legend: 'Tone',
        options: [
          { value: 'Gritty', label: 'Gritty' },
          { value: 'Hopeful', label: 'Hopeful' },
        ],
        modelValue: 'Gritty',
        'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
      },
    })
    expect(wrapper.find('legend').text()).toBe('Tone')
    const radios = wrapper.findAll('input[type="radio"]')
    expect(radios).toHaveLength(2)

    await radios[1]!.setValue(true)

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual(['Hopeful'])
  })
})
