import type { ReactNode } from 'react'
import { SECTIONS_END_ID } from '../utils/sections'

/**
 * The layout of SCR-STF-03 Staff account (DEC-118 (3), its Layout in docs/screens/SCR-STF-03.md, Width and Section
 * navigation): one column of at most 1024px, centred in the page area of SCR-ADM-01; the heading, then the notice of a
 * PENDING account (STF-09) at the column's width, before the cards at every width; from `xl` the navigation in a
 * sticky column of 200px beside the cards, below `xl` hidden with CSS (still mounted, so a hash still applies) and the
 * cards in the same order. The cards are 24px apart (`gap-6`), which the line of the navigation counts on
 * (hooks/use-active-section.ts), and the empty sentinel after them says when the end of the page is in view. It only
 * places the slots.
 */
export function StaffAccountLayout({
  heading,
  notice,
  nav,
  details,
  account,
}: {
  heading: ReactNode
  notice: ReactNode
  nav: ReactNode
  details: ReactNode
  account: ReactNode
}) {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      {heading}
      {notice}
      <div className="xl:grid xl:grid-cols-[200px_minmax(0,1fr)] xl:gap-8">
        <div className="hidden xl:sticky xl:top-6 xl:block xl:self-start">{nav}</div>
        <div className="flex min-w-0 flex-col gap-6">
          {details}
          {account}
          {/* -mt-6 takes back the gap, so that it sits at the bottom of the last card. */}
          <div id={SECTIONS_END_ID} aria-hidden="true" className="-mt-6" />
        </div>
      </div>
    </div>
  )
}
