import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useActiveSection } from './use-active-section'

// Which card of SCR-STF-03 the section navigation marks, and what a click on one of its links does
// (docs/screens/SCR-STF-03.md, Section navigation). jsdom lays nothing out, so an observer of this file reports what
// each test says, and the document's height is set by hand.

const IDS = ['info', 'account']
const END = 'page-end'

class FakeObserver {
  static instances: FakeObserver[] = []
  observed = new Set<Element>()
  disconnected = false
  constructor(
    readonly callback: IntersectionObserverCallback,
    readonly options?: IntersectionObserverInit,
  ) {
    FakeObserver.instances.push(this)
  }
  observe(element: Element) {
    this.observed.add(element)
  }
  unobserve(element: Element) {
    this.observed.delete(element)
  }
  disconnect() {
    this.disconnected = true
    this.observed.clear()
  }
  takeRecords() {
    return []
  }
  /**
   * Reports, for each id, whether its element intersects the observer's root (`true`), or else whether it has passed
   * above the root (`false`, as when the page has scrolled past it) or is still below the window (`'below'`).
   */
  report(states: Record<string, boolean | 'below'>) {
    const entries = Object.entries(states).map(
      ([id, state]) =>
        ({
          target: document.getElementById(id)!,
          isIntersecting: state === true,
          boundingClientRect: { top: state === 'below' ? 1000 : -200 },
          rootBounds: { top: 300 },
        }) as unknown as IntersectionObserverEntry,
    )
    act(() => this.callback(entries, this as unknown as IntersectionObserver))
  }
}

const observer = () => FakeObserver.instances.at(-1)!

/** The sections of the page, each with its h2, and the sentinel after them. */
function mountSections() {
  for (const id of IDS) {
    const section = document.createElement('section')
    section.id = id
    const title = document.createElement('h2')
    title.id = `${id}-title`
    title.tabIndex = -1
    section.append(title)
    document.body.append(section)
  }
  const end = document.createElement('div')
  end.id = END
  document.body.append(end)
}

/** Whether the document is taller than the window, so that it scrolls. */
function documentScrolls(scrolls: boolean) {
  Object.defineProperty(document.documentElement, 'scrollHeight', {
    configurable: true,
    value: scrolls ? 2400 : 600,
  })
  Object.defineProperty(window, 'innerHeight', { configurable: true, value: 900 })
}

beforeEach(() => {
  FakeObserver.instances = []
  vi.stubGlobal('IntersectionObserver', FakeObserver)
  documentScrolls(true)
})

const originalScrollIntoView = Element.prototype.scrollIntoView

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  Element.prototype.scrollIntoView = originalScrollIntoView
  window.history.replaceState(null, '', window.location.pathname)
  document.body.replaceChildren()
})

describe('the current card', () => {
  it('watches the cards and the sentinel from a third of the window down', () => {
    mountSections()
    renderHook(() => useActiveSection(IDS, END, true))

    expect(observer().options?.rootMargin).toBe('-33.333% 0px 0px 0px')
    expect([...observer().observed].map((element) => element.id)).toEqual(['info', 'account', END])
  })

  it('is the first card before anything is reported', () => {
    mountSections()
    const { result } = renderHook(() => useActiveSection(IDS, END, true))

    expect(result.current.currentId).toBe('info')
  })

  it('is the first card whose bottom is still below the line, as the page scrolls', () => {
    mountSections()
    const { result } = renderHook(() => useActiveSection(IDS, END, true))

    observer().report({ info: false, account: true, [END]: false })
    expect(result.current.currentId).toBe('account')

    observer().report({ info: true })
    expect(result.current.currentId).toBe('info')
  })

  it('is the last card once the sentinel comes into view', () => {
    mountSections()
    const { result } = renderHook(() => useActiveSection(IDS, END, true))

    observer().report({ info: true, account: true, [END]: true })

    expect(result.current.currentId).toBe('account')
  })

  it('stays the first card while every card is still below the window', () => {
    mountSections()
    const { result } = renderHook(() => useActiveSection(IDS, END, true))

    observer().report({ info: 'below', account: 'below', [END]: 'below' })

    expect(result.current.currentId).toBe('info')
  })

  it('is the last card once every card has passed above the line', () => {
    mountSections()
    const { result } = renderHook(() => useActiveSection(IDS, END, true))

    observer().report({ info: false, account: false, [END]: false })

    expect(result.current.currentId).toBe('account')
  })

  it('stays the first card when the page does not scroll', () => {
    documentScrolls(false)
    mountSections()
    const { result } = renderHook(() => useActiveSection(IDS, END, true))

    observer().report({ info: false, account: true, [END]: true })

    expect(result.current.currentId).toBe('info')
  })

  it('follows the cards that appear once the account has loaded', () => {
    const { result, rerender } = renderHook(({ ready }) => useActiveSection(IDS, END, ready), {
      initialProps: { ready: false },
    })
    expect(FakeObserver.instances.every((instance) => instance.observed.size === 0)).toBe(true)

    mountSections()
    rerender({ ready: true })
    observer().report({ info: false, account: true, [END]: false })

    expect(observer().observed.size).toBe(3)
    expect(result.current.currentId).toBe('account')
  })

  it('stops watching when the page goes', () => {
    mountSections()
    const { unmount } = renderHook(() => useActiveSection(IDS, END, true))

    unmount()

    expect(observer().disconnected).toBe(true)
  })
})

describe('a click on a link', () => {
  function select(reducedMotion: boolean) {
    mountSections()
    const scroll = vi.fn()
    Element.prototype.scrollIntoView = scroll
    vi.spyOn(window, 'matchMedia').mockReturnValue({ matches: reducedMotion } as MediaQueryList)
    const replace = vi.spyOn(window.history, 'replaceState')
    const push = vi.spyOn(window.history, 'pushState')
    const { result } = renderHook(() => useActiveSection(IDS, END, true))
    act(() => result.current.select('account'))
    return { scroll, replace, push }
  }

  it('scrolls smoothly to the card, focuses its title and replaces the hash', () => {
    const { scroll, replace, push } = select(false)

    expect(scroll).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' })
    expect(scroll.mock.contexts[0]).toBe(document.getElementById('account'))
    expect(document.activeElement).toBe(document.getElementById('account-title'))
    // `null`, so that Next.js's patched replaceState tells its router of the new URL.
    expect(replace).toHaveBeenCalledWith(null, '', '#account')
    expect(push).not.toHaveBeenCalled()
  })

  it('jumps at once when the user asks for reduced motion', () => {
    const { scroll } = select(true)

    expect(scroll).toHaveBeenCalledWith({ behavior: 'auto', block: 'start' })
  })
})
