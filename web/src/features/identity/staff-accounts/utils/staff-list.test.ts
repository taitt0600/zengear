// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import {
  apiDate,
  listHref,
  listParams,
  listRedirect,
  matchesSearch,
  nextSort,
  numbered,
  pageCount,
  sortDirection,
} from './staff-list'

describe('the URL of SCR-STF-01', () => {
  it('reads the status, sort and page, an empty value as none', () => {
    expect(listParams(new URLSearchParams('status=&sort=-name&page=2'))).toEqual({
      status: undefined,
      sort: '-name',
      page: '2',
    })
  })

  it('sets and removes values, keeping the others', () => {
    expect(
      listHref('/admin/staff', 'status=ACTIVE&page=3', { page: undefined, sort: 'name' }),
    ).toBe('/admin/staff?status=ACTIVE&sort=name')
  })

  it('is the path alone once no value is left', () => {
    expect(listHref('/admin/staff', 'page=2', { page: undefined })).toBe('/admin/staff')
  })
})

describe('where an answer sends the URL of SCR-STF-01', () => {
  const page = (count: number) => ({ items: Array.from({ length: count }, () => ({})) })

  it.each([
    ['a 400 with values in the URL resets them', new ApiError(400), 'status=X', undefined, true],
    ['a 400 with no value left does not', new ApiError(400), '', undefined, false],
    ['another failure does not', new ApiError(500), 'status=X', undefined, false],
  ])('%s', (_, error, url, result, resetting) => {
    expect(listRedirect(error, url, result, 0, 1).resetting).toBe(resetting)
  })

  it.each([
    ['an empty page past the end goes back to page 1', page(0), 5, 3, true],
    ['an empty first page stays', page(0), 0, 1, false],
    ['a page with rows stays', page(2), 22, 2, false],
    ['no answer yet stays', undefined, 0, 2, false],
  ])('%s', (_, result, totalCount, current, pastEnd) => {
    expect(listRedirect(null, '', result, totalCount, current).pastEnd).toBe(pastEnd)
  })
})

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

  it.each([
    [1, [1, 2]],
    [3, [41, 42]],
  ])('numbers the rows of page %i as %j', (page, numbers) => {
    const rows = numbered([{ name: 'An' }, { name: 'Bình' }], page)
    expect(rows.map((row) => row.number)).toEqual(numbers)
    expect(rows.map((row) => row.name)).toEqual(['An', 'Bình'])
  })

  it('numbers by the page size the API answered', () => {
    expect(numbered([{ name: 'An' }], 2, 10)[0].number).toBe(11)
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
