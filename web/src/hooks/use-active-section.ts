'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

/** Where a card (or the sentinel) is against the observer's root: passed above it, in it, or still below it. */
type Place = 'above' | 'in' | 'below'

/**
 * The card that the section navigation of a page of cards marks (SCR-STF-03 and SCR-ACC-01, their Layouts, Section
 * navigation), and what a click on a link does. The current card is the last one whose top has passed a line 24px plus
 * a third of the window from the top. `endId` is the empty sentinel the page's layout renders after its last card.
 *
 * One IntersectionObserver, no handler on scroll frames: its root starts a third of the window down, and since the
 * cards are 24px apart (`gap-6` of the layouts), "the bottom of a card leaves the root" is "the top of the next card
 * passes the line", so it reports exactly when the current card changes, and the current card is the first one still
 * in the root; when none is, the last one that has passed above it, or the first card while all are still below the
 * window (a tall notice above the cards). When the document scrolls, the last card is current once the sentinel after
 * it comes into view; when it does not scroll, the first one stays current. Whether it scrolls is read when the
 * observer reports: a resize that changes it with no card crossing the line shows only at the next report, accepted
 * while the pages have few cards, since the Layouts allow no scroll or resize handler.
 *
 * A clicked card is current at once and stays so until the page scrolls away from where that click left it
 * (`useHeldSection`), since the line cannot reach every card: on a tall screen the page does not scroll at all.
 *
 * `ready` is whether the page's data has loaded: the observer starts again then, since the cards replace their
 * Skeleton.
 */
export function useActiveSection(ids: string[], endId: string, ready: boolean) {
  const [places, setPlaces] = useState<Record<string, Place>>({})
  const { heldId, hold, release } = useHeldSection()
  const key = ids.join(' ')

  useEffect(() => {
    const targets = [...key.split(' '), endId]
      .map((id) => document.getElementById(id))
      .filter((element): element is HTMLElement => element !== null)
    if (targets.length === 0) return
    const observer = new IntersectionObserver(
      (entries) => {
        release()
        setPlaces((before) => {
          const next = { ...before }
          for (const entry of entries) next[entry.target.id] = placeOf(entry)
          return next
        })
      },
      { rootMargin: '-33.333% 0px 0px 0px' },
    )
    for (const target of targets) observer.observe(target)
    return () => observer.disconnect()
  }, [key, endId, ready, release])

  const select = useCallback(
    (id: string) => {
      const section = document.getElementById(id)
      if (!section) return
      const smooth = !window.matchMedia('(prefers-reduced-motion: reduce)').matches
      hold(id, section, smooth)
      // Without its own scroll, so that the focus does not cut the smooth scroll short.
      document.getElementById(`${id}-title`)?.focus({ preventScroll: true })
      // `null`, as Next.js documents: its patched replaceState then keeps its own state and tells the router of the
      // new URL; the router's own state (`__NA`) would bypass that, and the next router update would drop the hash.
      window.history.replaceState(null, '', `#${id}`)
    },
    [hold],
  )

  return { currentId: heldId ?? currentOf(ids, endId, places), select }
}

/**
 * The card a click made current, and the scroll to it. It is held until a report of the observer finds the page away
 * from where that click's own scroll left it (`release`), whatever the order of those reports and of `scrollend`:
 * after an instant scroll (reduced motion) or a click that needs none, that place is known at once; after a smooth
 * one, at its `scrollend`, which fires once when the scroll has completed and never when the position did not change
 * (MDN, Document: scrollend event), and gives way there when the user's own scroll left the page elsewhere; a browser
 * without it holds the target, so a report during the scroll gives way.
 */
function useHeldSection() {
  const [heldId, setHeldId] = useState<string | null>(null)
  const settling = useRef(false)
  const heldAt = useRef(0)
  const stopWaiting = useRef(() => {})
  useEffect(() => () => stopWaiting.current(), [])

  const hold = useCallback((id: string, section: HTMLElement, smooth: boolean) => {
    stopWaiting.current()
    const target = scrollTarget(section)
    const moves = Math.abs(target - window.scrollY) >= 1
    setHeldId(id)
    section.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto', block: 'start' })
    settling.current = smooth && moves && 'onscrollend' in window
    heldAt.current = smooth && moves ? target : window.scrollY
    if (!settling.current) return
    // Ended away from where the click sent it, the user scrolled during it: the line decides again.
    const settle = () => {
      settling.current = false
      if (Math.abs(window.scrollY - target) >= 1) setHeldId(null)
      else heldAt.current = window.scrollY
    }
    window.addEventListener('scrollend', settle, { once: true })
    stopWaiting.current = () => window.removeEventListener('scrollend', settle)
  }, [])

  const release = useCallback(() => {
    if (!settling.current && Math.abs(window.scrollY - heldAt.current) >= 1) setHeldId(null)
  }, [])

  return { heldId, hold, release }
}

/** Where the window's scroll to a section ends: its top at its `scroll-margin-top`, within what the page can scroll. */
function scrollTarget(section: HTMLElement): number {
  const margin = Number.parseFloat(getComputedStyle(section).scrollMarginTop) || 0
  const max = Math.max(0, document.documentElement.scrollHeight - viewportHeight())
  return Math.min(max, Math.max(0, window.scrollY + section.getBoundingClientRect().top - margin))
}

/** The height the document scrolls in: the window's without a horizontal scrollbar, which `innerHeight` counts. */
function viewportHeight(): number {
  return document.documentElement.clientHeight
}

/** An element out of the root is above it when its top is above the root's top (the window's, without root bounds). */
function placeOf(entry: IntersectionObserverEntry): Place {
  if (entry.isIntersecting) return 'in'
  return entry.boundingClientRect.top < (entry.rootBounds?.top ?? 0) ? 'above' : 'below'
}

/** The current id, from where each of the cards and the sentinel is against the observer's root. */
function currentOf(ids: string[], endId: string, places: Record<string, Place>): string {
  const first = ids[0]
  const last = ids[ids.length - 1]
  if (Object.keys(places).length === 0) return first
  if (document.documentElement.scrollHeight <= viewportHeight()) return first
  if (places[endId] === 'in') return last
  return (
    ids.find((id) => places[id] === 'in') ?? ids.findLast((id) => places[id] === 'above') ?? first
  )
}
