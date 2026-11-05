import type { StaffDto } from '@/lib/api/generated/model'

// The days of SCR-STF-03: today in Vietnam, which bounds the left date (R-USR-07, R-TZ-01), and the local dates of the
// calendar, which works in the browser's own day (react-day-picker without the time zones of date-fns, DEC-116 (6)).

const pad = (value: number) => String(value).padStart(2, '0')

// The parts of a day, not a locale's pattern, which can change with the browser's ICU data.
const VIETNAM_DAY = new Intl.DateTimeFormat('en', {
  timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** Today in Asia/Ho_Chi_Minh, as the API's days (`yyyy-MM-dd`). */
export function todayInVietnam(): string {
  const parts = Object.fromEntries(
    VIETNAM_DAY.formatToParts(new Date()).map(({ type, value }) => [type, value]),
  )
  return `${parts.year}-${parts.month}-${parts.day}`
}

/** A `yyyy-MM-dd` of the API as the local date the calendar shows for that day. */
export function calendarDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

/** The day a local date of the calendar stands for, as the API's days (`yyyy-MM-dd`). */
export function calendarDay(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** The line under the person's name: `email · employeeCode`, either alone when the other is empty. */
export function headline(account: Pick<StaffDto, 'email' | 'employeeCode'>): string {
  return [account.email, account.employeeCode].filter(Boolean).join(' · ')
}
