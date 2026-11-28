'use client'

import { useEffect, useRef } from 'react'

/**
 * Scrolls once to the card that the address's hash names, when the page's data has loaded, as a browser's own hash
 * would: not while the cards are Skeleton, whose heights would leave the card elsewhere (the Layouts of SCR-STF-03 and
 * SCR-ACC-01, Section navigation). A hash that names no card of `ids` is left alone.
 */
export function useHashOnLoad(ready: boolean, ids: string[]) {
  const applied = useRef(false)
  const key = ids.join(' ')
  useEffect(() => {
    if (!ready || applied.current) return
    applied.current = true
    const id = window.location.hash.slice(1)
    if (key.split(' ').includes(id)) document.getElementById(id)?.scrollIntoView({ block: 'start' })
  }, [ready, key])
}
