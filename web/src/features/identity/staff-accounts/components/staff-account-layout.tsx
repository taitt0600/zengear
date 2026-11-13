import type { ReactNode } from 'react'

/**
 * The layout of SCR-STF-03 Staff account (DEC-118 (3), its Layout in docs/screens/SCR-STF-03.md): a page of two cards,
 * its heading above them. From `lg`, the details over two thirds and the status over one third; below, one column with
 * the status first, so that its action comes before the details. It only places the slots.
 */
export function StaffAccountLayout({
  heading,
  details,
  status,
}: {
  heading: ReactNode
  details: ReactNode
  status: ReactNode
}) {
  return (
    <div className="flex flex-col gap-4 sm:gap-6">
      {heading}
      <div className="grid items-start gap-4 sm:gap-6 lg:grid-cols-3">
        <div className="min-w-0 lg:col-span-2">{details}</div>
        <div className="order-first min-w-0 lg:order-none">{status}</div>
      </div>
    </div>
  )
}
