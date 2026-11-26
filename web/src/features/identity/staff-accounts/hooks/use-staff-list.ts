'use client'

import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useMemo, useState } from 'react'
import type { GetApiV1AdminStaffParams } from '@/lib/api/generated/model'
import { useGetApiV1AdminStaff } from '@/lib/api/generated/staff/staff'
import {
  DEFAULT_SORT,
  listHref,
  listParams,
  listRedirect,
  matchesSearch,
  nextSort,
  numbered,
  PAGE_SIZE,
  pageCount,
  type SortField,
} from '../utils/staff-list'

/** The status links of SCR-STF-01: every account, or one status; PENDING is the tab Chờ duyệt (STF-09). */
export type StatusFilter = 'all' | 'ACTIVE' | 'DISABLED' | 'PENDING'

/**
 * The state of SCR-STF-01 (STF-05, STF-09): GET /api/v1/admin/staff with the status, sort and page of the URL, 20 rows
 * a page, the search over the rows of that page, and the URL changes of the status links, the sort and the pages. The search text
 * stays in the page, never in the URL (DEC-105).
 */
export function useStaffList() {
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const url = searchParams.toString()
  const { status, sort, page } = listParams(searchParams)

  // The URL's values as they come: the API checks them (R-USR-13), and its 400 resets the URL below.
  const params = { status, sort, page, pageSize: PAGE_SIZE } as GetApiV1AdminStaffParams
  const query = useGetApiV1AdminStaff(params)
  const result = query.data?.data
  const totalCount = Number(result?.totalCount ?? 0)
  const currentPage = Number(result?.page ?? 1)

  // The pages of the last answer, kept while the next one loads, so that the search hint does not come and go.
  const [knownPages, setKnownPages] = useState(1)
  const pages = result ? pageCount(totalCount) : knownPages
  if (pages !== knownPages) setKnownPages(pages)

  const [search, setSearch] = useState('')
  // Numbered once per answer, by the page and page size the API answered; the search then only hides rows.
  const numberedRows = useMemo(
    () => (result ? numbered(result.items, currentPage, Number(result.pageSize)) : []),
    [result, currentPage],
  )
  const rows = useMemo(
    () => numberedRows.filter((row) => matchesSearch(row, search)),
    [numberedRows, search],
  )

  const href = useCallback(
    (changes: Record<string, string | undefined>) => listHref(pathname, url, changes),
    [pathname, url],
  )
  const go = (changes: Record<string, string | undefined>) =>
    router.push(href(changes), { scroll: false })

  const { resetting, pastEnd } = listRedirect(query.error, url, result, totalCount, currentPage)
  useEffect(() => {
    if (resetting) router.replace(pathname)
    else if (pastEnd) router.replace(href({ page: undefined }))
  }, [resetting, pastEnd, router, pathname, href])

  return {
    // A failed refresh of a list already shown keeps it: the problem shows only when there is nothing to show.
    error: resetting || result ? null : query.error,
    isFetching: query.isFetching,
    retry: () => void query.refetch(),
    // The answer is in, but the URL is about to change (a reset, or back to page 1): show it as loading.
    loaded: !!result && !resetting && !pastEnd,
    rows,
    totalCount,
    page: currentPage,
    pages,
    status: (status ?? 'all') as StatusFilter,
    filtered: status !== undefined,
    sort: sort ?? DEFAULT_SORT,
    search,
    setSearch,
    pending: status === 'PENDING',
    // A status link drops the page and the sort, as the Layout of SCR-STF-01 draws it: the tab Chờ duyệt has no column
    // that sorts (STF-09).
    statusHref: (value: StatusFilter) =>
      href({ status: value === 'all' ? undefined : value, page: undefined, sort: undefined }),
    toggleSort: (field: SortField) =>
      go({ sort: nextSort(sort ?? DEFAULT_SORT, field), page: undefined }),
    // "Xóa bộ lọc" clears the search and the status; only a change of status leaves the page (back to 1). On the tab
    // Chờ duyệt it clears the search only: the tab is where the approver works, not a filter (STF-09).
    clearFilters: () => {
      setSearch('')
      if (status !== undefined && status !== 'PENDING') go({ status: undefined, page: undefined })
    },
    pageHref: (target: number) => href({ page: target > 1 ? String(target) : undefined }),
  }
}

export type StaffList = ReturnType<typeof useStaffList>
