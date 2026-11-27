'use client'

import { useTranslations } from 'next-intl'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useDay } from '../hooks/use-staff-texts'
import { StaffAccountRow } from './staff-account-row'
import { StaffPhoto } from './staff-photo'

const SKELETON_WIDTHS = ['w-56', 'w-20', 'w-28', 'w-24', 'w-24']

/**
 * The card "Hồ sơ nhân viên" of SCR-STF-03: the staff profile, each empty value "—", and the photo block (the initials
 * until gap 8). A `section` the section navigation reaches (`#info`), also while it is Skeleton, so that the observer
 * finds it; named by its h2 once loaded. "Cập nhật thông tin" in its CardAction comes with gap 7. From `@xl` of the card
 * the photo sits in a column of its own at the right, below it centred above the rows; it comes first in the DOM, as
 * the narrow layout shows it. Nothing when the account cannot show (the heading shows why).
 */
export function StaffAccountDetailsSection() {
  const t = useTranslations()
  const showDay = useDay()
  const { account, error } = useStaffAccountState()
  if (error) return null

  const day = (value: string | null) => (value ? showDay(value) : null)
  const rows: [string, string | null][] = account
    ? [
        [t('staff.list.columns.name'), account.name],
        [t('staff.list.columns.employeeCode'), account.employeeCode],
        [t('staff.account.workPhone'), account.workPhone],
        [t('staff.list.columns.joinedDate'), day(account.joinedDate)],
        [t('staff.list.columns.leftDate'), day(account.leftDate)],
      ]
    : []

  return (
    <section
      id="info"
      aria-labelledby={account ? 'info-title' : undefined}
      aria-busy={account ? undefined : true}
      className="scroll-mt-6"
    >
      <Card>
        <CardHeader>
          {account ? (
            <>
              <CardTitle>
                <h2 id="info-title" tabIndex={-1} className="focus:outline-none">
                  {t('staff.account.info.title')}
                </h2>
              </CardTitle>
              <CardDescription>{t('staff.account.info.description')}</CardDescription>
            </>
          ) : (
            <>
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-4 w-96 max-w-full" />
            </>
          )}
        </CardHeader>
        <CardContent className="@container">
          <div className="grid gap-6 @xl:grid-cols-[minmax(0,1fr)_200px] @xl:gap-8">
            <div className="flex justify-center @xl:col-start-2 @xl:row-start-1 @xl:border-l @xl:pt-3 @xl:pl-8">
              {account ? (
                <StaffPhoto name={account.name} />
              ) : (
                <Skeleton className="size-24 rounded-full @xl:size-40" />
              )}
            </div>
            <dl className="min-w-0 divide-y @xl:row-start-1">
              {account
                ? rows.map(([label, value]) => (
                    <StaffAccountRow key={label} label={label}>
                      {value ?? t('staff.list.noValue')}
                    </StaffAccountRow>
                  ))
                : SKELETON_WIDTHS.map((width, index) => (
                    <div key={index} className="flex gap-4 py-3">
                      <Skeleton className="h-5 w-24" />
                      <Skeleton className={`h-5 ${width}`} />
                    </div>
                  ))}
            </dl>
          </div>
        </CardContent>
      </Card>
    </section>
  )
}
