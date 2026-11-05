import { Badge } from '@/components/ui/badge'
import type { UserStatus } from '@/lib/api/generated/model'

/** The label of each status of an account (`useStaffStatusTexts`). */
export type StaffStatusTexts = Record<UserStatus, string>

const VARIANT = { ACTIVE: 'success', DISABLED: 'secondary', ANONYMIZED: 'outline' } as const

/**
 * Presentational: the status of an account (SCR-STF-01, SCR-STF-03), soft badges chosen by the owner on the Layout of
 * SCR-STF-03: soft green while active, grey once disabled, an outline once anonymized.
 */
export function StaffStatusBadge({
  status,
  texts,
}: {
  status: UserStatus
  texts: StaffStatusTexts
}) {
  return <Badge variant={VARIANT[status]}>{texts[status]}</Badge>
}
