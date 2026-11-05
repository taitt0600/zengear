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
 * Presentational: what the list of SCR-STF-01 shows in place of its rows when it has none to show (no account, nothing
 * matches, a failed load), an Empty with an outline inside the card, so that the filters stay (Grouping of
 * docs/SCREENS.md). `tone="error"` is a failure: destructive, and announced.
 */
export function StaffListEmpty({
  icon,
  title,
  description,
  action,
  tone,
}: {
  icon: ReactNode
  title: string
  description?: string
  action?: ReactNode
  tone?: 'error'
}) {
  const error = tone === 'error'
  return (
    <Empty className="border">
      <EmptyHeader role={error ? 'alert' : undefined}>
        <EmptyMedia variant="icon" className={cn(error && 'text-destructive')}>
          {icon}
        </EmptyMedia>
        <EmptyTitle className={cn(error && 'text-destructive')}>{title}</EmptyTitle>
        {description && <EmptyDescription>{description}</EmptyDescription>}
      </EmptyHeader>
      {action && <EmptyContent>{action}</EmptyContent>}
    </Empty>
  )
}
