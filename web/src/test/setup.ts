import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Testing Library unmounts after each test by itself only when Vitest globals are on; they are not.
afterEach(cleanup)

// jsdom has none of these: the sidebar reads the screen width through matchMedia (hooks/use-mobile.ts), answered here
// from window.innerWidth, an open tooltip measures its arrow with a ResizeObserver (Radix), and the section navigation of
// SCR-STF-03 follows the cards with an IntersectionObserver, which never reports here (its own test stubs one that
// does). Not in the node tests.
if (typeof window !== 'undefined') {
  window.matchMedia ??= (query: string) => {
    const max = /max-width:\s*(\d+)px/.exec(query)
    return {
      matches: max ? window.innerWidth <= Number(max[1]) : false,
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    }
  }
  window.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  window.IntersectionObserver ??= class implements IntersectionObserver {
    readonly root = null
    readonly rootMargin = ''
    readonly scrollMargin = ''
    readonly thresholds = []
    observe() {}
    unobserve() {}
    disconnect() {}
    takeRecords() {
      return []
    }
  }
}
