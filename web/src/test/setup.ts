import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Testing Library unmounts after each test by itself only when Vitest globals are on; they are not.
afterEach(cleanup)

// jsdom has neither: the sidebar reads the screen width through matchMedia (hooks/use-mobile.ts), answered here from
// window.innerWidth, and an open tooltip measures its arrow with a ResizeObserver (Radix). Not in the node tests.
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
}
