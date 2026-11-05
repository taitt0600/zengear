import type { ReactNode } from 'react'
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from '@/components/ui/empty'
import { cn } from '@/lib/utils'

/**
 * Presentational: an Empty with an outline (Grouping of docs/SCREENS.md) of the staff screens: what the list of
 * SCR-STF-01 shows in place of its rows when it has none to show (no account, nothing matches, a failed load), inside
 * the card so that the filters stay; and, with `heading`, the page of SCR-STF-03 when its account cannot show (a 404, a
 * failed load), its title then the page's h1. `tone="error"` is a failure: destructive, and announced.
 */
export function StaffListEmpty({
  icon,
  title,
  description,
  action,
  tone,
  heading,
}: {
  icon: ReactNode
  title: string
  description?: string
  action?: ReactNode
  tone?: 'error'
  heading?: boolean
}) {
  const error = tone === 'error'
  return (
    <Empty className="border">
      <EmptyHeader role={error ? 'alert' : undefined}>
        <EmptyMedia variant="icon" className={cn(error && 'text-destructive')}>
          {icon}
        </EmptyMedia>
        <EmptyTitle className={cn(error && 'text-destructive')}>
          {heading ? <h1>{title}</h1> : title}
        </EmptyTitle>
        {description && <EmptyDescription>{description}</EmptyDescription>}
      </EmptyHeader>
      {action && <EmptyContent>{action}</EmptyContent>}
    </Empty>
  )
}
