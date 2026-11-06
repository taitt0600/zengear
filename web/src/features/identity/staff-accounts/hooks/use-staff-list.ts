'use client'

import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useMemo, useState } from 'react'
import type { GetApiV1AdminStaffParams } from '@/lib/api/generated/model'
import { useGetApiV1AdminStaff } from '@/lib/api/generated/staff/staff'
import { ApiError } from '@/lib/api/problems'
import {
  DEFAULT_SORT,
  matchesSearch,
  nextSort,
  numbered,
  PAGE_SIZE,
  pageCount,
  type SortField,
} from '../utils/staff-list'

/** The status filter of the Select: every account, or one status. */
export type StatusFilter = 'all' | 'ACTIVE' | 'DISABLED'

/**
 * The state of SCR-STF-01 (STF-05): GET /api/v1/admin/staff with the status, sort and page of the URL, 20 rows a page,
 * the search over the rows of that page, and the URL changes of the filters, the sort and the pages. The search text
 * stays in the page, never in the URL (DEC-105).
 */
export function useStaffList() {
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const url = searchParams.toString()
  // An empty value (`?status=`) is no value: the default.
  const status = searchParams.get('status') || undefined
  const sort = searchParams.get('sort') || undefined
  const page = searchParams.get('page') || undefined

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
    (changes: Record<string, string | undefined>) => {
      const next = new URLSearchParams(url)
      for (const [key, value] of Object.entries(changes)) {
        if (value === undefined) next.delete(key)
        else next.set(key, value)
      }
      const text = next.toString()
      return text ? `${pathname}?${text}` : pathname
    },
    [pathname, url],
  )
  const go = (changes: Record<string, string | undefined>) =>
    router.push(href(changes), { scroll: false })

  // A URL edited by hand: back to the default filters. With nothing left to reset, the 400 shows like any failure.
  const resetting = query.error instanceof ApiError && query.error.status === 400 && url !== ''
  // A page past the end (an account disabled or gone since the link was made): back to page 1.
  const pastEnd = !!result && result.items.length === 0 && totalCount > 0 && currentPage > 1
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
    setStatus: (value: StatusFilter) =>
      go({ status: value === 'all' ? undefined : value, page: undefined }),
    toggleSort: (field: SortField) =>
      go({ sort: nextSort(sort ?? DEFAULT_SORT, field), page: undefined }),
    // "Xóa bộ lọc" clears the search and the status; only a change of status leaves the page (back to 1).
    clearFilters: () => {
      setSearch('')
      if (status !== undefined) go({ status: undefined, page: undefined })
    },
    pageHref: (target: number) => href({ page: target > 1 ? String(target) : undefined }),
  }
}

export type StaffList = ReturnType<typeof useStaffList>
