import { Badge } from '@/components/ui/badge'
import type { StaffRoleDto } from '@/lib/api/generated/model'

/** Presentational: the roles of an account (SCR-STF-01), one badge each, by the names the API gives. */
export function StaffRolesCell({ roles }: { roles: StaffRoleDto[] }) {
  return (
    <div className="flex gap-1">
      {roles.map((role) => (
        <Badge key={role.id} variant="secondary">
          {role.name}
        </Badge>
      ))}
    </div>
  )
}
