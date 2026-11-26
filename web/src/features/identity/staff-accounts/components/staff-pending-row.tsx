import { CheckIcon, Undo2Icon } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { TableCell, TableRow } from '@/components/ui/table'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import type { ApprovalAction } from '../hooks/use-staff-approval'
import { staffPath } from '../utils/paths'
import { StaffRolesCell } from './staff-roles-cell'

/** The texts of a PENDING row, read once by the table. */
export type StaffPendingRowTexts = {
  noValue: string
  approve: string
  reject: string
  withdraw: string
  view: string
  label: (action: ApprovalAction | 'view', name: string) => string
}

/**
 * Presentational: one PENDING account of the tab Chờ duyệt of SCR-STF-01 (STF-09, UC-140): STT, its name (a link to
 * SCR-STF-03) with the email under it, its roles, the creator and the time of creation (DEC-137 (1)), then "Từ chối"
 * and "Duyệt" when the API says the user may approve it (`canApprove`), "Thu hồi" and "Xem chi tiết" for its creator
 * (`canWithdraw`, DEC-140 (1)), or "Xem chi tiết" alone. The flags only hide buttons: the API checks again.
 */
export function StaffPendingRow({
  row,
  texts,
  dateTime,
  onAction,
}: {
  row: StaffListItemDto & { number: number }
  texts: StaffPendingRowTexts
  dateTime: (value: string) => string
  onAction: (action: ApprovalAction, row: StaffListItemDto) => void
}) {
  const view = (
    <Button asChild variant="outline" size="sm">
      <Link href={staffPath(row.id)} aria-label={texts.label('view', row.name)}>
        {texts.view}
      </Link>
    </Button>
  )
  return (
    <TableRow>
      <TableCell className="text-right tabular-nums">{row.number}</TableCell>
      <TableCell>
        <Link href={staffPath(row.id)} className="font-medium underline underline-offset-4">
          {row.name}
        </Link>
        <div className="text-muted-foreground text-sm">{row.email}</div>
      </TableCell>
      <TableCell>
        <StaffRolesCell roles={row.roles} />
      </TableCell>
      <TableCell>{row.createdBy?.name ?? texts.noValue}</TableCell>
      <TableCell className="tabular-nums">
        {row.createdAt ? dateTime(row.createdAt) : texts.noValue}
      </TableCell>
      <TableCell>
        <div className="flex gap-2">
          {row.canApprove && (
            <>
              <Button
                variant="destructive-soft"
                size="sm"
                aria-label={texts.label('reject', row.name)}
                onClick={() => onAction('reject', row)}
              >
                {texts.reject}
              </Button>
              <Button
                variant="outline"
                size="sm"
                aria-label={texts.label('approve', row.name)}
                onClick={() => onAction('approve', row)}
              >
                <CheckIcon aria-hidden />
                {texts.approve}
              </Button>
            </>
          )}
          {!row.canApprove && row.canWithdraw && (
            <Button
              variant="destructive-soft"
              size="sm"
              aria-label={texts.label('withdraw', row.name)}
              onClick={() => onAction('withdraw', row)}
            >
              <Undo2Icon aria-hidden />
              {texts.withdraw}
            </Button>
          )}
          {!row.canApprove && view}
        </div>
      </TableCell>
    </TableRow>
  )
}
