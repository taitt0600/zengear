'use client'

import Link from 'next/link'
import { useTranslations } from 'next-intl'
import { useEffect, useId, useRef } from 'react'
import { Badge } from '@/components/ui/badge'
import { usePendingStaffCount } from '@/hooks/use-pending-staff-count'
import { cn } from '@/lib/utils'
import type { StaffList, StatusFilter } from '../hooks/use-staff-list'

const LINKS = {
  all: 'all',
  ACTIVE: 'active',
  DISABLED: 'disabled',
  PENDING: 'pending',
} as const satisfies Record<StatusFilter, string>

/**
 * The status links of SCR-STF-01 (STF-09): one control that shows every status and the work waiting, Tất cả, Đang hoạt
 * động, Đã khóa and Chờ duyệt with the number of accounts waiting (hidden at 0). Links styled like shadcn's segmented
 * control, not tabs: each changes the URL and has no panel; the current one is `aria-current="page"`. On a narrow
 * screen the track scrolls sideways, the current link scrolled into view.
 */
export function StaffStatusNav({ list }: { list: Pick<StaffList, 'status' | 'statusHref'> }) {
  const t = useTranslations('staff.list.status')
  const labelId = useId()
  const count = usePendingStaffCount(true)
  const current = useRef<HTMLAnchorElement>(null)
  // A body, not an expression: Chromium's scrollIntoView returns a Promise, which React would take for the clean-up.
  useEffect(() => {
    current.current?.scrollIntoView?.({ block: 'nearest', inline: 'nearest' })
  }, [list.status])

  return (
    <nav aria-labelledby={labelId} className="flex min-w-0 flex-col gap-2">
      <span id={labelId} className="text-sm font-medium">
        {t('label')}
      </span>
      <div className="bg-muted flex w-fit max-w-full gap-1 overflow-x-auto rounded-lg p-1">
        {(Object.entries(LINKS) as [StatusFilter, (typeof LINKS)[StatusFilter]][]).map(
          ([value, key]) => {
            const isCurrent = list.status === value
            return (
              <Link
                key={value}
                ref={isCurrent ? current : undefined}
                href={list.statusHref(value)}
                scroll={false}
                aria-current={isCurrent ? 'page' : undefined}
                className={cn(
                  'text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 flex shrink-0 items-center gap-2 rounded-md px-3 py-1 text-sm font-medium whitespace-nowrap outline-none focus-visible:ring-[3px]',
                  isCurrent && 'bg-background text-foreground shadow-sm',
                )}
              >
                {t(key)}
                {value === 'PENDING' && count !== undefined && count > 0 && (
                  <Badge>
                    <span aria-hidden>{count}</span>
                    <span className="sr-only">{t('pendingCount', { count })}</span>
                  </Badge>
                )}
              </Link>
            )
          },
        )}
      </div>
    </nav>
  )
}
