'use client'

import { useGetApiV1AdminStaff } from '@/lib/api/generated/staff/staff'

/** The one query of the count: the same parameters, hence the same query key, wherever it shows. */
const PENDING_COUNT = { status: 'PENDING', pageSize: 1 } as const

/**
 * The number of staff accounts waiting for approval (STF-09): `totalCount` of the list with `status=PENDING` and
 * `pageSize=1`, shared by "Chờ duyệt" of the admin menu (SCR-ADM-01) and of the status links of SCR-STF-01, so that the
 * invalidation of the staff list after an approval, a rejection or a withdrawal updates both. Undefined while it loads,
 * when it fails and when `enabled` is false: the count never holds up the entry that shows it.
 */
export function usePendingStaffCount(enabled: boolean): number | undefined {
  const query = useGetApiV1AdminStaff(PENDING_COUNT, { query: { enabled } })
  const total = query.data?.data.totalCount
  return enabled && total !== undefined ? Number(total) : undefined
}
