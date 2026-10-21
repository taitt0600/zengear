import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useId } from 'react'
import { cn } from '@/lib/utils'
import { visibleMenu, type MenuEntry } from './admin-menu'

/** The menu of SCR-ADM-01, limited to the entries the user's permissions allow. */
export function AdminNav({
  permissions,
  onNavigate,
}: {
  permissions: readonly string[]
  onNavigate?: () => void
}) {
  const t = useTranslations('admin.menu')
  const pathname = usePathname()
  // The menu renders twice on narrow screens (aside and sheet): ids must stay unique.
  const id = useId()

  const link = (entry: MenuEntry) => {
    const current = pathname === entry.href || pathname.startsWith(`${entry.href}/`)
    return (
      <li key={entry.key}>
        <Link
          href={entry.href}
          aria-current={current ? 'page' : undefined}
          onClick={onNavigate}
          className={cn(
            'block rounded-md px-3 py-2 text-sm hover:bg-accent hover:text-accent-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
            current && 'bg-accent font-medium text-accent-foreground',
          )}
        >
          {t(entry.key)}
        </Link>
      </li>
    )
  }

  return (
    <nav aria-label={t('title')} className="flex flex-col gap-1 p-3">
      <ul className="flex flex-col gap-1">
        {visibleMenu(permissions).map((item) =>
          'entries' in item ? (
            <li key={item.key} className="mt-3 flex flex-col gap-1">
              <span
                id={`${id}-${item.key}`}
                className="px-3 text-xs font-medium text-muted-foreground"
              >
                {t(item.key)}
              </span>
              <ul aria-labelledby={`${id}-${item.key}`} className="flex flex-col gap-1">
                {item.entries.map(link)}
              </ul>
            </li>
          ) : (
            link(item)
          ),
        )}
      </ul>
    </nav>
  )
}
