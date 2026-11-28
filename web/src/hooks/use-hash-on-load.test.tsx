import { renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useHashOnLoad } from './use-hash-on-load'

// The hash of a card in the address of SCR-STF-03 or SCR-ACC-01 (their Layouts, Section navigation): applied once
// the page's data has loaded, as a browser's own hash would be.

const IDS = ['profile', 'password']
const originalScrollIntoView = Element.prototype.scrollIntoView
let scroll = vi.fn<Element['scrollIntoView']>()

beforeEach(() => {
  for (const id of IDS) {
    const section = document.createElement('section')
    section.id = id
    document.body.append(section)
  }
  scroll = vi.fn<Element['scrollIntoView']>()
  Element.prototype.scrollIntoView = scroll
})

afterEach(() => {
  Element.prototype.scrollIntoView = originalScrollIntoView
  window.history.replaceState(null, '', window.location.pathname)
  document.body.replaceChildren()
})

describe('the hash on load', () => {
  it('scrolls to the card it names once the data has loaded, not before', () => {
    window.history.replaceState(null, '', '#password')
    const { rerender } = renderHook(({ ready }) => useHashOnLoad(ready, IDS), {
      initialProps: { ready: false },
    })
    expect(scroll).not.toHaveBeenCalled()

    rerender({ ready: true })

    expect(scroll).toHaveBeenCalledExactlyOnceWith({ block: 'start' })
    expect(scroll.mock.contexts[0]).toBe(document.getElementById('password'))
  })

  it('scrolls only once, whatever renders after', () => {
    window.history.replaceState(null, '', '#password')
    const { rerender } = renderHook(({ ready }) => useHashOnLoad(ready, IDS), {
      initialProps: { ready: true },
    })

    rerender({ ready: false })
    rerender({ ready: true })

    expect(scroll).toHaveBeenCalledOnce()
  })

  it('leaves a hash that names no card alone', () => {
    window.history.replaceState(null, '', '#elsewhere')
    renderHook(() => useHashOnLoad(true, IDS))

    expect(scroll).not.toHaveBeenCalled()
  })
})
