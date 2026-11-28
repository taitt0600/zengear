import { useId, type MouseEvent } from 'react'
import { cn } from '@/lib/utils'

export type SectionNavItem = { id: string; label: string }

/**
 * Presentational: the section navigation of a page of cards (SCR-STF-03 and SCR-ACC-01, their Layouts, Section
 * navigation), named by its visible title, one link per card in page order, the current one marked. It is navigation
 * on the page ground, not a card (an exception to Grouping). A plain click is handed to `onSelect`, which scrolls and
 * moves the focus (`useActiveSection` of `@/hooks`); a click with a modifier or another button keeps the link's own
 * behaviour (a new tab, a new window).
 */
export function SectionNav({
  label,
  items,
  currentId,
  onSelect,
}: {
  label: string
  items: SectionNavItem[]
  currentId: string
  onSelect: (id: string) => void
}) {
  const titleId = useId()
  const follow = (id: string) => (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
      return
    event.preventDefault()
    onSelect(id)
  }

  return (
    <nav aria-labelledby={titleId} className="flex flex-col gap-1">
      <p
        id={titleId}
        className="flex h-8 items-center px-2 text-xs font-medium text-muted-foreground"
      >
        {label}
      </p>
      <ul className="flex flex-col gap-1">
        {items.map((item) => {
          const current = item.id === currentId
          return (
            <li key={item.id}>
              <a
                href={`#${item.id}`}
                aria-current={current ? 'location' : undefined}
                onClick={follow(item.id)}
                className={cn(
                  'flex min-h-8 items-center rounded-md px-2 py-1 text-sm wrap-break-word text-muted-foreground outline-none hover:bg-accent focus-visible:ring-[3px] focus-visible:ring-ring/50',
                  current && 'bg-accent font-medium text-foreground',
                )}
              >
                {item.label}
              </a>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}
