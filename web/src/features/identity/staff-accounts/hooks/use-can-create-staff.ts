'use client'

import { useCurrentUser } from '@/features/identity/access'

/** Whether the user may create staff accounts: only hides the way to SCR-STF-02, the API still checks (R-SEC-03). */
export function useCanCreateStaff(): boolean {
  return useCurrentUser().permissions.includes('staff.create')
}
