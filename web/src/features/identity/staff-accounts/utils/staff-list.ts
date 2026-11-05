import type { GetApiV1AdminStaffSort, StaffListItemDto } from '@/lib/api/generated/model'

// The rows of a page of SCR-STF-01 (docs/SCREENS.md) and the sort the API takes when the URL names none (DEC-105).
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

/** The pages of `totalCount` accounts, one at least, so that an empty list is page 1 of 1. */
export function pageCount(totalCount: number, pageSize = PAGE_SIZE): number {
  return Math.max(1, Math.ceil(totalCount / pageSize))
}

/** A `yyyy-MM-dd` of the API as the start of that day in Vietnam, to show it in Asia/Ho_Chi_Minh. */
export function apiDate(value: string): Date {
  return new Date(`${value}T00:00:00+07:00`)
}
