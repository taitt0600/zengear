import { useId, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

/**
 * Presentational: a row of the description lists of the cards of SCR-STF-03, inside a `dl` with `divide-y` in a
 * CardContent that is a `@container`. From `@sm` (the card, not the window, since the menu changes its width) the
 * label in 136px beside the value, and the action, when there is one, at the end of the row; below it the label over
 * the value, then the action at full width. A long value (an email) wraps, never cut short. The value is named by its
 * label, so that it is the definition of that term by role and name, not only by its place after the `dt`.
 */
export function StaffAccountRow({
  label,
  children,
  action,
}: {
  label: string
  children: ReactNode
  action?: ReactNode
}) {
  const labelId = useId()
  return (
    <div
      className={cn(
        'flex flex-col gap-1 py-3 @sm:grid @sm:gap-x-4',
        action
          ? '@sm:grid-cols-[136px_minmax(0,1fr)_auto] @sm:items-start'
          : '@sm:grid-cols-[136px_minmax(0,1fr)]',
      )}
    >
      <dt id={labelId} className="text-sm text-muted-foreground">
        {label}
      </dt>
      <dd aria-labelledby={labelId} className="min-w-0 text-sm wrap-anywhere">
        {children}
      </dd>
      {action && (
        <dd className="mt-2 flex flex-col gap-2 @sm:mt-0 @sm:flex-row *:w-full @sm:*:w-auto">
          {action}
        </dd>
      )}
    </div>
  )
}
