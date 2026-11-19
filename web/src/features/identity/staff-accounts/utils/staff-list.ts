import type { GetApiV1AdminStaffSort, StaffListItemDto } from '@/lib/api/generated/model'
import { ApiError } from '@/lib/api/problems'

// The rows of a page of SCR-STF-01 (docs/screens/SCR-STF-01.md) and the sort the API takes when the URL names none
// (DEC-105).
export const PAGE_SIZE = 20
export const DEFAULT_SORT = 'name'

/** The columns that sort: "Họ tên", "Mã nhân viên", "Ngày vào làm". */
export type SortField = 'name' | 'employeeCode' | 'joinedDate'

/** A click on a column: by that column, then the same with `-` (descending), then ascending again. */
export function nextSort(current: string, field: SortField): GetApiV1AdminStaffSort {
  return current === field ? (`-${field}` as const) : field
}

/** The `aria-sort` of a column under the current sort. */
export function sortDirection(
  current: string,
  field: SortField,
): 'ascending' | 'descending' | 'none' {
  if (current === field) return 'ascending'
  if (current === `-${field}`) return 'descending'
  return 'none'
}

const fold = (text: string) => text.normalize('NFC').toLocaleLowerCase('vi')

/**
 * Whether a loaded row holds the search text in its name, employee code or email, ignoring case. The search stays in
 * the browser: names and emails never go into the query string (DEC-105).
 */
export function matchesSearch(
  row: Pick<StaffListItemDto, 'name' | 'employeeCode' | 'email'>,
  text: string,
): boolean {
  const needle = fold(text.trim())
  if (!needle) return true
  return [row.name, row.employeeCode, row.email].some(
    (value) => value && fold(value).includes(needle),
  )
}

/**
 * The rows of a page with their "STT": (page − 1) × the page size (20) + their index in the page's `items`, 1 first.
 * Numbered before the search, which then hides rows without renumbering the others.
 */
export function numbered<T>(
  items: T[],
  page: number,
  pageSize = PAGE_SIZE,
): (T & { number: number })[] {
  return items.map((item, index) => ({ ...item, number: (page - 1) * pageSize + index + 1 }))
}

/** The pages of `totalCount` accounts, one at least, so that an empty list is page 1 of 1. */
export function pageCount(totalCount: number, pageSize = PAGE_SIZE): number {
  return Math.max(1, Math.ceil(totalCount / pageSize))
}

/** The status, sort and page of the URL as they come; an empty value (`?status=`) is no value: the default. */
export function listParams(searchParams: Pick<URLSearchParams, 'get'>) {
  return {
    status: searchParams.get('status') || undefined,
    sort: searchParams.get('sort') || undefined,
    page: searchParams.get('page') || undefined,
  }
}

/** The list's URL with `changes` applied to its query `url`: a value `undefined` removes that key. */
export function listHref(
  pathname: string,
  url: string,
  changes: Record<string, string | undefined>,
): string {
  const next = new URLSearchParams(url)
  for (const [key, value] of Object.entries(changes)) {
    if (value === undefined) next.delete(key)
    else next.set(key, value)
  }
  const text = next.toString()
  return text ? `${pathname}?${text}` : pathname
}

/** Whether an answer sends the URL back: to the default filters (`resetting`), or to page 1 (`pastEnd`). */
export function listRedirect(
  error: unknown,
  url: string,
  result: { items: unknown[] } | undefined,
  totalCount: number,
  page: number,
) {
  return {
    // A URL edited by hand: back to the default filters. With nothing left to reset, the 400 shows like any failure.
    resetting: error instanceof ApiError && error.status === 400 && url !== '',
    // A page past the end (an account disabled or gone since the link was made): back to page 1.
    pastEnd: !!result && result.items.length === 0 && totalCount > 0 && page > 1,
  }
}

/** How a day shows on SCR-STF-01 and SCR-STF-03: `dd/MM/yyyy`, through `useDay`. */
export const DAY = { day: '2-digit', month: '2-digit', year: 'numeric' } as const

/** A `yyyy-MM-dd` of the API as the start of that day in Vietnam, to show it in Asia/Ho_Chi_Minh. */
export function apiDate(value: string): Date {
  return new Date(`${value}T00:00:00+07:00`)
}
