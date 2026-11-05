// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { apiDate, matchesSearch, nextSort, pageCount, sortDirection } from './staff-list'

describe('the sort of SCR-STF-01', () => {
  it.each([
    ['name', 'name', '-name'],
    ['-name', 'name', 'name'],
    ['name', 'employeeCode', 'employeeCode'],
    ['-joinedDate', 'employeeCode', 'employeeCode'],
    ['-employeeCode', 'employeeCode', 'employeeCode'],
  ] as const)('from %s, a click on %s sorts by %s', (current, field, next) => {
    expect(nextSort(current, field)).toBe(next)
  })

  it.each([
    ['name', 'name', 'ascending'],
    ['-name', 'name', 'descending'],
    ['joinedDate', 'name', 'none'],
  ] as const)('with %s, the column %s is %s', (current, field, direction) => {
    expect(sortDirection(current, field)).toBe(direction)
  })
})

describe('the search over the loaded rows', () => {
  const row = { name: 'Nguyễn Văn An', employeeCode: 'NV0012', email: 'an@zengear.test' }

  it.each(['nguyễn', 'VĂN AN', '  an  ', 'nv00', 'ZENGEAR.TEST'])('finds %j', (text) => {
    expect(matchesSearch(row, text)).toBe(true)
  })

  it('finds a name typed in decomposed Unicode', () => {
    expect(matchesSearch(row, 'Nguyễn'.normalize('NFD'))).toBe(true)
  })

  it('keeps every row for an empty search', () => {
    expect(matchesSearch(row, '   ')).toBe(true)
  })

  it('skips a row that matches nothing, and an empty code or email', () => {
    expect(matchesSearch(row, 'bình')).toBe(false)
    expect(matchesSearch({ name: 'Chủ cửa hàng', employeeCode: null, email: null }, 'nv')).toBe(
      false,
    )
  })
})

describe('the pages of the list', () => {
  it.each([
    [0, 1],
    [20, 1],
    [21, 2],
    [45, 3],
  ])('%i accounts make %i pages of 20', (total, pages) => {
    expect(pageCount(total)).toBe(pages)
  })
})

describe('a date of the API', () => {
  it('is that day in Vietnam, whatever the time zone of the machine', () => {
    const date = apiDate('2026-10-01')
    const shown = new Intl.DateTimeFormat('vi', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(date)
    expect(shown).toBe('01/10/2026')
  })
})
