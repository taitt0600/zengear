'use client'

import { useCurrentUser } from '@/features/identity/access'

/**
 * Whether the user may lock and restore staff accounts (`staff.disable`), on SCR-STF-01 and SCR-STF-03: only hides the
 * commands, the API still checks (R-SEC-03) and refuses the user's own account (R-USR-12).
 */
export function useCanDisableStaff(): boolean {
  return useCurrentUser().permissions.includes('staff.disable')
}
