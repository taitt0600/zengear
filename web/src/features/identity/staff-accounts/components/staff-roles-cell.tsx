import { Badge } from '@/components/ui/badge'
import type { StaffRoleDto } from '@/lib/api/generated/model'

/**
 * Presentational: the roles of an account (SCR-STF-01, SCR-STF-03), one badge each, by the names the API gives; they
 * wrap on a narrow card instead of widening it.
 */
export function StaffRolesCell({ roles }: { roles: StaffRoleDto[] }) {
  return (
    <div className="flex flex-wrap gap-1">
      {roles.map((role) => (
        <Badge key={role.id} variant="secondary">
          {role.name}
        </Badge>
      ))}
    </div>
  )
}
