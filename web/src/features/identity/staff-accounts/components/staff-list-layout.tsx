import type { ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'

/**
 * The layout of SCR-STF-01 Staff list (DEC-118 (3), its Layout in docs/SCREENS.md): one card, a page of one card whose
 * header holds the h1, then the filters above the list. It holds no data.
 */
export function StaffListLayout({
  header,
  filters,
  list,
}: {
  header: ReactNode
  filters: ReactNode
  list: ReactNode
}) {
  return (
    <Card>
      {header}
      <CardContent className="flex flex-col gap-4">
        {filters}
        {list}
      </CardContent>
    </Card>
  )
}
