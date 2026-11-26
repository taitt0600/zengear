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
    PENDING: t('list.status.pending'),
  }
}

/** How a day of the API shows: `dd/MM/yyyy`, that day in Vietnam. */
export function useDay(): (value: string) => string {
  const format = useFormatter()
  return (value) => format.dateTime(apiDate(value), DAY)
}

/**
 * How a time of the API (UTC) shows: `dd/MM/yyyy HH:mm` in Asia/Ho_Chi_Minh, the time zone of next-intl; the day and
 * the time are formatted apart so that the order stays the same whatever the locale writes.
 */
export function useDateTime(): (value: string) => string {
  const format = useFormatter()
  return (value) => {
    const date = new Date(value)
    return `${format.dateTime(date, DAY)} ${format.dateTime(date, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })}`
  }
}
