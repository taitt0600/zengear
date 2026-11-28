import type { ReactNode } from 'react'
import { SECTIONS_END_ID } from '../utils/sections'

/**
 * The layout of SCR-ACC-01 My account (DEC-118 (3), its Layout in docs/screens/SCR-ACC-01.md, Width and section
 * navigation, as SCR-STF-03): one column of at most 1024px, centred in the page area of SCR-ADM-01; the heading, then
 * from `xl` the navigation in a sticky column of 200px beside the cards, below `xl` hidden with CSS (still mounted, so a
 * hash still applies) and the cards stacked, profile first. The cards are 24px apart (`gap-6`), which the line of the
 * navigation counts on (`useActiveSection` of `@/hooks`), and the empty sentinel after them says when the end of the
 * page is in view. It only places the slots.
 */
export function AccountLayout({
  heading,
  nav,
  profile,
  password,
}: {
  heading: ReactNode
  nav: ReactNode
  profile: ReactNode
  password: ReactNode
}) {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      {heading}
      <div className="xl:grid xl:grid-cols-[200px_minmax(0,1fr)] xl:gap-8">
        <div className="hidden xl:sticky xl:top-6 xl:block xl:self-start">{nav}</div>
        <div className="flex min-w-0 flex-col gap-6">
          {profile}
          {password}
          {/* -mt-6 takes back the gap, so that it sits at the bottom of the last card. */}
          <div id={SECTIONS_END_ID} aria-hidden="true" className="-mt-6" />
        </div>
      </div>
    </div>
  )
}
