'use client'

import { useCallback, useEffect, useState } from 'react'
import { SECTIONS_END_ID } from '../utils/sections'

/** Where a card (or the sentinel) is against the observer's root: passed above it, in it, or still below it. */
type Place = 'above' | 'in' | 'below'

/**
 * The card of SCR-STF-03 that the section navigation marks (its Layout, Section navigation), and what a click on a link
 * does. The current card is the last one whose top has passed a line 24px plus a third of the window from the top.
 *
 * One IntersectionObserver, no handler on scroll frames: its root starts a third of the window down, and since the
 * cards are 24px apart (`gap-6` of the layout), "the bottom of a card leaves the root" is "the top of the next card
 * passes the line", so it reports exactly when the current card changes, and the current card is the first one still
 * in the root; when none is, the last one that has passed above it, or the first card while all are still below the
 * window (a tall notice above the cards). When the document scrolls, the last card is current once the sentinel after
 * it comes into view; when it does not scroll, the first one stays current. Whether it scrolls is read when the
 * observer reports: a resize that changes it with no card crossing the line shows only at the next report, accepted
 * while the page has few cards, since the Layout allows no scroll or resize handler.
 *
 * `ready` is whether the account has loaded: the observer starts again then, since the cards replace their Skeleton.
 * From the build of SCR-ACC-01, a clicked card stays current until the next scroll; not yet.
 */
export function useActiveSection(ids: string[], ready: boolean) {
  const [places, setPlaces] = useState<Record<string, Place>>({})
  const key = ids.join(' ')

  useEffect(() => {
    const targets = [...key.split(' '), SECTIONS_END_ID]
      .map((id) => document.getElementById(id))
      .filter((element): element is HTMLElement => element !== null)
    if (targets.length === 0) return
    const observer = new IntersectionObserver(
      (entries) =>
        setPlaces((before) => {
          const next = { ...before }
          for (const entry of entries) next[entry.target.id] = placeOf(entry)
          return next
        }),
      { rootMargin: '-33.333% 0px 0px 0px' },
    )
    for (const target of targets) observer.observe(target)
    return () => observer.disconnect()
  }, [key, ready])

  const select = useCallback((id: string) => {
    const section = document.getElementById(id)
    if (!section) return
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    section.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' })
    // Without its own scroll, so that the focus does not cut the smooth scroll short.
    document.getElementById(`${id}-title`)?.focus({ preventScroll: true })
    // `null`, as Next.js documents: its patched replaceState then keeps its own state and tells the router of the new
    // URL; the router's own state (`__NA`) would bypass that, and the next router update would drop the hash.
    window.history.replaceState(null, '', `#${id}`)
  }, [])

  return { currentId: currentOf(ids, places), select }
}

/** An element out of the root is above it when its top is above the root's top (the window's, without root bounds). */
function placeOf(entry: IntersectionObserverEntry): Place {
  if (entry.isIntersecting) return 'in'
  return entry.boundingClientRect.top < (entry.rootBounds?.top ?? 0) ? 'above' : 'below'
}

/** The current id, from where each of the cards and the sentinel is against the observer's root. */
function currentOf(ids: string[], places: Record<string, Place>): string {
  const first = ids[0]
  const last = ids[ids.length - 1]
  if (Object.keys(places).length === 0) return first
  if (document.documentElement.scrollHeight <= window.innerHeight) return first
  if (places[SECTIONS_END_ID] === 'in') return last
  return (
    ids.find((id) => places[id] === 'in') ?? ids.findLast((id) => places[id] === 'above') ?? first
  )
}
