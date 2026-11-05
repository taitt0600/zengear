'use client'

import { useFormatter, useTranslations } from 'next-intl'
import type { UserStatus } from '@/lib/api/generated/model'
import { apiDate, DAY } from '../utils/staff-list'

// The texts SCR-STF-01 and SCR-STF-03 show the same way, in one place.

/** The label of each status of an account, for `StaffStatusBadge`. */
export function useStaffStatusTexts(): Record<UserStatus, string> {
  const t = useTranslations('staff')
  return {
    ACTIVE: t('list.status.active'),
    DISABLED: t('list.status.disabled'),
    ANONYMIZED: t('account.status.anonymized'),
  }
}

/** How a day of the API shows: `dd/MM/yyyy`, that day in Vietnam. */
export function useDay(): (value: string) => string {
  const format = useFormatter()
  return (value) => format.dateTime(apiDate(value), DAY)
}
