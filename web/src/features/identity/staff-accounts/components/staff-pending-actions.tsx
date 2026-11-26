'use client'

import { CheckIcon, Undo2Icon, XIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import { useStaffApproval, type ApprovalAction } from '../hooks/use-staff-approval'
import { StaffApprovalDialog } from './staff-approval-dialog'

/**
 * The menu items of a PENDING row of SCR-STF-01 (STF-09), split out of `StaffRowActions` for size: "Duyệt tài khoản"
 * and "Từ chối" when the API says the user may approve it (`canApprove`), or "Thu hồi" for its creator (`canWithdraw`,
 * DEC-140 (1)); never "Khóa tài khoản" (R-USR-17: 409). The flags only hide items: the API checks again.
 */
export function StaffPendingActions({
  row,
  onChoose,
}: {
  row: Pick<StaffListItemDto, 'canApprove' | 'canWithdraw'>
  onChoose: (action: ApprovalAction) => void
}) {
  const t = useTranslations('staff.list.actions')
  if (row.canApprove) {
    return (
      <>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => onChoose('approve')}>
          <CheckIcon aria-hidden />
          {t('approve')}
        </DropdownMenuItem>
        <DropdownMenuItem variant="destructive" onSelect={() => onChoose('reject')}>
          <XIcon aria-hidden />
          {t('reject')}
        </DropdownMenuItem>
      </>
    )
  }
  if (row.canWithdraw) {
    return (
      <>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onSelect={() => onChoose('withdraw')}>
          <Undo2Icon aria-hidden />
          {t('withdraw')}
        </DropdownMenuItem>
      </>
    )
  }
  return null
}

/**
 * The confirmation of the command a PENDING row of SCR-STF-01 chose, with its request (hooks/use-staff-approval.ts),
 * sent with the item's `version` as `If-Match` (DEC-140 (2)). Mounted only once a row chose a command, so that the rows
 * that never use one hold no mutation.
 */
export function StaffPendingDialog({
  action,
  row,
  open,
  onOpenChange,
  onClosed,
}: {
  action: ApprovalAction
  row: StaffListItemDto
  open: boolean
  onOpenChange: (open: boolean) => void
  onClosed: () => void
}) {
  const approval = useStaffApproval()
  return (
    <StaffApprovalDialog
      action={action}
      account={row}
      open={open}
      pending={approval.pending}
      onOpenChange={onOpenChange}
      onConfirm={() =>
        approval.run(action, { id: row.id, name: row.name, version: row.version ?? undefined })
      }
      onClosed={onClosed}
    />
  )
}
