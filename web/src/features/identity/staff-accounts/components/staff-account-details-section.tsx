'use client'

import { useTranslations } from 'next-intl'
import type { ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useDay } from '../hooks/use-staff-texts'
import { StaffRolesCell } from './staff-roles-cell'

const SKELETON_WIDTHS = ['w-56', 'w-20', 'w-28', 'w-24', 'w-24', 'w-24']

/**
 * The card "Thông tin nhân viên" of SCR-STF-03: what was recorded when the account was created, each empty value "—".
 * No field is editable in Phase 1: "Cập nhật thông tin" comes with gap 7. Nothing when the account cannot show (the
 * heading shows why).
 */
export function StaffAccountDetailsSection() {
  const t = useTranslations()
  const showDay = useDay()
  const { account, error } = useStaffAccountState()
  if (error) return null

  const day = (value: string | null) => (value ? showDay(value) : null)
  const fields: [string, ReactNode][] = account
    ? [
        [t('staff.list.columns.email'), account.email],
        [t('staff.list.columns.employeeCode'), account.employeeCode],
        [t('staff.account.workPhone'), account.workPhone],
        [t('staff.list.columns.roles'), <StaffRolesCell key="roles" roles={account.roles} />],
        [t('staff.list.columns.joinedDate'), day(account.joinedDate)],
        [t('staff.list.columns.leftDate'), day(account.leftDate)],
      ]
    : []

  return (
    <Card aria-busy={account ? undefined : true}>
      <CardHeader>
        {account ? (
          <>
            <CardTitle>
              <h2>{t('staff.account.info.title')}</h2>
            </CardTitle>
            <CardDescription>{t('staff.account.info.description')}</CardDescription>
          </>
        ) : (
          <>
            <Skeleton className="h-5 w-44" />
            <Skeleton className="h-4 w-80 max-w-full" />
          </>
        )}
      </CardHeader>
      <CardContent>
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 sm:gap-y-5">
          {account
            ? fields.map(([label, value]) => (
                <div key={label} className="flex min-w-0 flex-col gap-1">
                  <dt className="text-sm text-muted-foreground">{label}</dt>
                  {/* `anywhere`, not `break-word`: a long email wraps instead of widening the card (the Layout). */}
                  <dd className="text-sm wrap-anywhere">{value ?? t('staff.list.noValue')}</dd>
                </div>
              ))
            : SKELETON_WIDTHS.map((width, index) => (
                <div key={index} className="flex flex-col gap-2">
                  <Skeleton className="h-4 w-24" />
                  <Skeleton className={`h-4 ${width}`} />
                </div>
              ))}
        </dl>
      </CardContent>
    </Card>
  )
}
