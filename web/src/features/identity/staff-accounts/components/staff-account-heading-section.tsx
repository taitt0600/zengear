'use client'

import { ArrowLeftIcon, TriangleAlertIcon, UserXIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { adminRoutes } from '@/features/identity/access'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useProblemText } from '@/hooks/use-problem-toast'
import { initials } from '@/utils/initials'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useStaffStatusTexts } from '../hooks/use-staff-texts'
import { headline } from '../utils/staff-account'
import { StaffListEmpty } from './staff-list-empty'
import { StaffStatusBadge } from './staff-status-badge'

/**
 * The heading of SCR-STF-03: the person's avatar (their initials until gap 8 gives a photo), their name as the h1 with
 * the status, and their email and employee code. When the account cannot show, the page is this section's Empty: the
 * API's text of a 404 with the way back to the list, or of any other failure with "Thử lại".
 */
export function StaffAccountHeadingSection() {
  const t = useTranslations()
  const problemText = useProblemText()
  const statusTexts = useStaffStatusTexts()
  const { account, notFound, error, loading, isFetching, retry } = useStaffAccountState()

  if (error) {
    return notFound ? (
      <StaffListEmpty
        heading
        icon={<UserXIcon aria-hidden />}
        title={problemText(error)}
        action={
          <Button asChild>
            <Link href={adminRoutes.staff}>
              <ArrowLeftIcon data-icon="inline-start" aria-hidden />
              {t('staff.account.backToList')}
            </Link>
          </Button>
        }
      />
    ) : (
      <StaffListEmpty
        heading
        tone="error"
        icon={<TriangleAlertIcon aria-hidden />}
        title={problemText(error)}
        action={
          <Button onClick={retry} disabled={isFetching}>
            {t('admin.retry')}
          </Button>
        }
      />
    )
  }
  if (loading || !account) {
    return (
      <div className="flex items-center gap-3 sm:gap-4">
        <Skeleton className="size-14 rounded-full sm:size-16" />
        <Skeleton className="h-7 w-56" />
        <Skeleton className="h-5 w-24 rounded-full" />
      </div>
    )
  }
  const letters = initials(account.name)
  return (
    <div className="flex items-center gap-3 sm:gap-4">
      <Avatar className="size-14 sm:size-16" aria-hidden>
        <AvatarFallback className="text-xl font-medium">{letters}</AvatarFallback>
      </Avatar>
      <div className="flex min-w-0 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2 sm:gap-3">
          <h1 className="text-xl font-semibold">{account.name}</h1>
          <StaffStatusBadge status={account.status} texts={statusTexts} />
        </div>
        <p className="text-sm wrap-break-word text-muted-foreground">{headline(account)}</p>
      </div>
    </div>
  )
}
