'use client'

import { useTranslations } from 'next-intl'
import { useRef } from 'react'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useFocusAfterApproval } from '../hooks/use-focus-after-approval'
import { StaffAccountRow } from './staff-account-row'
import { StaffRolesCell } from './staff-roles-cell'
import { StaffStatusRow } from './staff-status-row'

const SKELETON_WIDTHS = ['w-60', 'w-28', 'w-32', 'w-20']

/**
 * The card "Tài khoản" of SCR-STF-03 (STF-02): the sign-in account, its email, roles, status with the actions that
 * change it in its row, and whether the default password is changed. A `section` the section navigation reaches
 * (`#account`), also while it is Skeleton; named by its h2 once loaded. The row of the last reset link with "Gửi link
 * đặt lại mật khẩu" comes with gap 9 (STF-12). Nothing when the account cannot show.
 */
export function StaffAccountLoginSection() {
  const t = useTranslations('staff')
  const { account, error } = useStaffAccountState()
  const heading = useRef<HTMLHeadingElement>(null)
  useFocusAfterApproval(account?.status, heading)
  if (error) return null

  return (
    <section
      id="account"
      aria-labelledby={account ? 'account-title' : undefined}
      aria-busy={account ? undefined : true}
      className="scroll-mt-6"
    >
      <Card>
        <CardHeader>
          {account ? (
            <>
              <CardTitle>
                <h2 id="account-title" ref={heading} tabIndex={-1} className="focus:outline-none">
                  {t('account.accountCard.title')}
                </h2>
              </CardTitle>
              <CardDescription>{t('account.accountCard.description')}</CardDescription>
            </>
          ) : (
            <>
              <Skeleton className="h-5 w-28" />
              <Skeleton className="h-4 w-96 max-w-full" />
            </>
          )}
        </CardHeader>
        <CardContent className="@container">
          <dl className="divide-y">
            {account ? (
              <>
                <StaffAccountRow label={t('account.accountCard.email')}>
                  {account.email}
                </StaffAccountRow>
                <StaffAccountRow label={t('list.columns.roles')}>
                  <StaffRolesCell roles={account.roles} />
                </StaffAccountRow>
                <StaffStatusRow account={account} heading={heading} />
                <StaffAccountRow label={t('account.passwordCard.default')}>
                  {account.mustChangePassword ? (
                    <Badge variant="outline">{t('account.passwordCard.unchanged')}</Badge>
                  ) : (
                    <Badge variant="secondary">{t('account.passwordCard.changed')}</Badge>
                  )}
                </StaffAccountRow>
              </>
            ) : (
              SKELETON_WIDTHS.map((width, index) => (
                <div key={index} className="flex gap-4 py-3">
                  <Skeleton className="h-5 w-24" />
                  <Skeleton className={`h-5 ${width}`} />
                </div>
              ))
            )}
          </dl>
        </CardContent>
      </Card>
    </section>
  )
}
