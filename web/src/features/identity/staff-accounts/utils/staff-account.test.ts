import { afterEach, describe, expect, it, vi } from 'vitest'
import { calendarDate, calendarDay, headline, todayInVietnam } from './staff-account'

afterEach(() => {
  vi.useRealTimers()
})

describe('todayInVietnam', () => {
  it('is the day in Asia/Ho_Chi_Minh, which starts 7 hours before UTC', () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-11-05T16:59:00Z'))
    expect(todayInVietnam()).toBe('2026-11-05')
    vi.setSystemTime(new Date('2026-11-05T17:00:00Z'))
    expect(todayInVietnam()).toBe('2026-11-06')
  })
})

describe('calendarDate and calendarDay', () => {
  it('turn a day of the API into the local date the calendar shows, and back', () => {
    const date = calendarDate('2026-03-09')
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2026, 2, 9])
    expect(calendarDay(date)).toBe('2026-03-09')
    expect(calendarDay(new Date(2026, 11, 31, 23, 59))).toBe('2026-12-31')
  })
})

describe('headline', () => {
  it.each([
    [
      { email: 'manager.fixture@zengear.test', employeeCode: 'FX0001' },
      'manager.fixture@zengear.test · FX0001',
    ],
    [{ email: 'owner@zengear.test', employeeCode: null }, 'owner@zengear.test'],
    [{ email: null, employeeCode: 'NV0012' }, 'NV0012'],
    [{ email: null, employeeCode: null }, ''],
  ])('%o gives %s', (account, text) => {
    expect(headline(account)).toBe(text)
  })
})
