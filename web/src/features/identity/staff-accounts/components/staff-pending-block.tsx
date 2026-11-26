import { CheckIcon, Undo2Icon } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import type { ApprovalAction } from '../hooks/use-staff-approval'
import { staffPath } from '../utils/paths'
import type { StaffPendingRowTexts } from './staff-pending-row'
import { StaffRolesCell } from './staff-roles-cell'

/**
 * Presentational: one PENDING account of the tab Chờ duyệt on a narrow screen (STF-09): its name and email, its roles
 * and "Tạo bởi {name} · {time}", then the buttons of `StaffPendingRow` at full width, side by side.
 */
export function StaffPendingBlock({
  row,
  texts,
  createdBy,
  onAction,
}: {
  row: StaffListItemDto
  texts: StaffPendingRowTexts
  createdBy: string | undefined
  onAction: (action: ApprovalAction, row: StaffListItemDto) => void
}) {
  return (
    <li className="flex flex-col gap-3 rounded-lg border p-4">
      <div>
        <Link href={staffPath(row.id)} className="font-medium underline underline-offset-4">
          {row.name}
        </Link>
        <div className="text-muted-foreground text-sm break-all">{row.email}</div>
      </div>
      <StaffRolesCell roles={row.roles} />
      {createdBy && <p className="text-muted-foreground text-sm">{createdBy}</p>}
      <div className="grid grid-cols-2 gap-2">
        {row.canApprove ? (
          <>
            <Button
              variant="destructive-soft"
              aria-label={texts.label('reject', row.name)}
              onClick={() => onAction('reject', row)}
            >
              {texts.reject}
            </Button>
            <Button
              variant="outline"
              aria-label={texts.label('approve', row.name)}
              onClick={() => onAction('approve', row)}
            >
              <CheckIcon aria-hidden />
              {texts.approve}
            </Button>
          </>
        ) : (
          <>
            {row.canWithdraw && (
              <Button
                variant="destructive-soft"
                aria-label={texts.label('withdraw', row.name)}
                onClick={() => onAction('withdraw', row)}
              >
                <Undo2Icon aria-hidden />
                {texts.withdraw}
              </Button>
            )}
            <Button
              asChild
              variant="outline"
              className={row.canWithdraw ? undefined : 'col-span-2'}
            >
              <Link href={staffPath(row.id)} aria-label={texts.label('view', row.name)}>
                {texts.view}
              </Link>
            </Button>
          </>
        )}
      </div>
    </li>
  )
}
