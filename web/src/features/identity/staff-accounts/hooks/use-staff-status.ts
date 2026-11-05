'use client'

import { useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import {
  getGetApiV1AdminStaffIdQueryKey,
  getGetApiV1AdminStaffQueryKey,
} from '@/lib/api/generated/staff/staff'
import {
  usePostApiV1AdminStaffIdDisable,
  usePostApiV1AdminStaffIdRestore,
} from '@/lib/api/generated/staff-status/staff-status'
import { sessionOutcome } from '@/features/identity/access'
import { ApiError, hasProblemType } from '@/lib/api/problems'

// The commands of STF-02 on one account, by its id, which SCR-STF-03 and the row actions of SCR-STF-01 share: after
// them the account and every page of the staff list load again, so that both screens show the new status
// (docs/FRONTEND.md, Data from the API: invalidate, never edit the cache by hand).

/**
 * Whether a refused command leaves the account in a state the page may not show: gone (404), changed by someone else
 * (409 concurrency-conflict), or unknown (a 500, or no answer: it may be disabled with its sessions still open, R-SEC-04).
 * An answer about the session is left to the admin layout's rules: a reload would only fail the same way.
 */
export function reloadsAfter(error: unknown): boolean {
  if (sessionOutcome(error)) return false
  if (!(error instanceof ApiError)) return true
  return (
    error.status === 404 ||
    error.status >= 500 ||
    hasProblemType(error.problem, 'concurrency-conflict')
  )
}

/** Loads the account and the staff list again; resolves once the account's answer is in. */
function useReloadStaff(id: string) {
  const client = useQueryClient()
  return useCallback(
    () =>
      Promise.all([
        client.invalidateQueries({ queryKey: getGetApiV1AdminStaffIdQueryKey(id) }),
        client.invalidateQueries({ queryKey: getGetApiV1AdminStaffQueryKey() }),
      ]),
    [client, id],
  )
}

/** The options both commands take: reload after a 200 (awaited, so the caller goes on with the new account) and after
 * a refusal that `reloadsAfter` names. */
function useReloadOptions(id: string) {
  const reload = useReloadStaff(id)
  return {
    onSuccess: async () => {
      await reload()
    },
    onError: (error: unknown) => {
      if (reloadsAfter(error)) void reload()
    },
  }
}

/** POST /api/v1/admin/staff/{id}/disable `{ leftDate }` (R-USR-07, R-USR-12, R-SEC-04). */
export function useDisableStaff(id: string) {
  return usePostApiV1AdminStaffIdDisable({ mutation: useReloadOptions(id) })
}

/** POST /api/v1/admin/staff/{id}/restore (R-USR-03, R-USR-12). */
export function useRestoreStaff(id: string) {
  return usePostApiV1AdminStaffIdRestore({ mutation: useReloadOptions(id) })
}
