'use client'

import { useQuery } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Skeleton } from '@/components/ui/skeleton'
import { meQuery } from '@/features/identity/access'
import { initials } from '@/utils/initials'
import { headline } from '../utils/account'

/**
 * The heading of SCR-ACC-01: the staff member's avatar (their initials until gap 8 gives a photo), hidden from screen
 * readers since the line beside it names them; the h1 "Tài khoản của tôi", always shown; and a muted line with their
 * name and email from GET /me, which wraps, never cut short. While GET /me loads, the avatar and the line are Skeleton.
 */
export function AccountHeadingSection() {
  const t = useTranslations('admin.user')
  const me = useQuery(meQuery).data
  return (
    <div className="flex items-center gap-3 sm:gap-4">
      {me ? (
        <Avatar className="size-14 sm:size-16" aria-hidden>
          <AvatarFallback className="text-xl font-medium">{initials(me.name)}</AvatarFallback>
        </Avatar>
      ) : (
        <Skeleton className="size-14 shrink-0 rounded-full sm:size-16" />
      )}
      <div className="flex min-w-0 flex-col gap-1">
        <h1 className="text-xl font-semibold">{t('account')}</h1>
        {me ? (
          <p className="text-sm wrap-anywhere text-muted-foreground">{headline(me)}</p>
        ) : (
          <Skeleton className="h-4 w-72 max-w-full" />
        )}
      </div>
    </div>
  )
}
