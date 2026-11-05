import { Badge } from '@/components/ui/badge'
import type { UserStatus } from '@/lib/api/generated/model'

/** Presentational: the status of an account (SCR-STF-01), green while active and red once disabled. */
export function StaffStatusBadge({ status, label }: { status: UserStatus; label: string }) {
  return <Badge variant={status === 'ACTIVE' ? 'success' : 'destructive'}>{label}</Badge>
}
