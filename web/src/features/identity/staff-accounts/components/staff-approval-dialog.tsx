'use client'

import { LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import type { ApprovalAction } from '../hooks/use-staff-approval'
import { useDateTime } from '../hooks/use-staff-texts'
import { closedFocus } from '../utils/closed-focus'
import { StaffRolesCell } from './staff-roles-cell'

/** What the confirmation repeats of the account (SCR-STF-01, SCR-STF-03). */
export type ApprovalAccount = Pick<
  StaffListItemDto,
  'id' | 'name' | 'email' | 'roles' | 'createdBy' | 'createdAt'
>

const DIALOG = {
  approve: 'approveDialog',
  reject: 'rejectDialog',
  withdraw: 'withdrawDialog',
} as const

/**
 * The confirmations "Duyệt tài khoản này?", "Từ chối tài khoản này?" and "Thu hồi tài khoản này?" (STF-09, UC-140,
 * DEC-140 (1)), the same on SCR-STF-01 and SCR-STF-03: they repeat the name, the email, the roles and the creator with
 * the time, then send the command. One component for the three, which differ only in their texts and the submit's
 * variant. The submit is a Button, not AlertDialogAction, which would close before the answer comes: it is disabled with
 * a spinner while the request runs, and the dialog closes once `onConfirm` resolves. `onClosed` puts the focus back.
 */
export function StaffApprovalDialog({
  action,
  account,
  open,
  pending,
  onOpenChange,
  onConfirm,
  onClosed,
}: {
  action: ApprovalAction
  account: ApprovalAccount
  open: boolean
  pending: boolean
  onOpenChange: (open: boolean) => void
  onConfirm: () => Promise<void>
  onClosed: () => void
}) {
  const t = useTranslations('staff.account')
  const dateTime = useDateTime()
  const dialog = DIALOG[action]

  async function confirm() {
    await onConfirm()
    onOpenChange(false)
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent onCloseAutoFocus={closedFocus(onClosed)}>
        <AlertDialogHeader>
          <AlertDialogTitle>{t(`${dialog}.title`)}</AlertDialogTitle>
          <AlertDialogDescription>{t(`${dialog}.description`)}</AlertDialogDescription>
        </AlertDialogHeader>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
          <dt className="text-muted-foreground">{t('summary.name')}</dt>
          <dd className="font-medium">{account.name}</dd>
          <dt className="text-muted-foreground">{t('summary.email')}</dt>
          <dd className="break-all">{account.email}</dd>
          <dt className="text-muted-foreground">{t('summary.roles')}</dt>
          <dd>
            <StaffRolesCell roles={account.roles} />
          </dd>
          {account.createdBy && account.createdAt && (
            <>
              <dt className="text-muted-foreground">{t('summary.createdBy')}</dt>
              <dd>
                {t('summary.createdByValue', {
                  name: account.createdBy.name,
                  time: dateTime(account.createdAt),
                })}
              </dd>
            </>
          )}
        </dl>
        <AlertDialogFooter>
          <AlertDialogCancel>{t('cancel')}</AlertDialogCancel>
          <Button
            variant={action === 'approve' ? 'default' : 'destructive-soft'}
            onClick={() => void confirm()}
            disabled={pending}
          >
            {pending && (
              <LoaderCircleIcon className="animate-spin" data-icon="inline-start" aria-hidden />
            )}
            {t(`${dialog}.submit`)}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
