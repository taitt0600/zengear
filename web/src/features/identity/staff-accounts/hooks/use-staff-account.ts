'use client'

import { useCurrentUser } from '@/features/identity/access'
import { useGetApiV1AdminStaffId } from '@/lib/api/generated/staff/staff'
import { ApiError } from '@/lib/api/problems'

/**
 * The state of SCR-STF-03 (STF-01, STF-02): GET /api/v1/admin/staff/{id}, and whether the user may lock or restore it.
 * Hiding only: the API checks the permission and refuses the user's own account (R-SEC-03, R-USR-12).
 */
export function useStaffAccount(id: string) {
  const me = useCurrentUser()
  const query = useGetApiV1AdminStaffId(id)
  const account = query.data?.data
  // A 404 wins over what the cache still holds: the account is gone (a reload after a refused command).
  const notFound = query.error instanceof ApiError && query.error.status === 404

  return {
    id,
    account: notFound ? undefined : account,
    notFound,
    // A failed refresh of an account already shown keeps it: the problem shows only when there is nothing to show,
    // or when it is a 404.
    error: notFound || !account ? query.error : null,
    loading: !account && !query.error,
    isFetching: query.isFetching,
    retry: () => void query.refetch(),
    canDisable: me.permissions.includes('staff.disable'),
    isOwn: account?.id === me.id,
  }
}

export type StaffAccount = ReturnType<typeof useStaffAccount>
