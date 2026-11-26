'use client'

import { useQueryClient } from '@tanstack/react-query'
import { CheckIcon, ClockIcon, Undo2Icon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useRouter } from 'next/navigation'
import { useRef, useState } from 'react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { adminRoutes } from '@/features/identity/access'
import type { StaffDto } from '@/lib/api/generated/model'
import { getGetApiV1AdminStaffIdQueryKey } from '@/lib/api/generated/staff/staff'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useStaffApproval, type ApprovalAction } from '../hooks/use-staff-approval'
import { useDateTime } from '../hooks/use-staff-texts'
import { StaffApprovalDialog } from './staff-approval-dialog'

/** SCR-STF-01 on its tab Chờ duyệt, where the page goes once the account no longer exists. */
const PENDING_TAB = `${adminRoutes.staff}?status=PENDING`

/**
 * The notice of a PENDING account on SCR-STF-03 (STF-09, UC-140 steps 2 and 3, DEC-138, DEC-140): who created it and
 * when, that the staff member cannot sign in yet, then "Từ chối" and "Duyệt" when the API says the user may approve it
 * (`canApprove`), "Thu hồi" for its creator (`canWithdraw`), or a sentence saying another person approves it. Each opens
 * the confirmation of SCR-STF-01, sent with the account's ETag. After an approval the account reloads and the notice
 * goes; after a rejection or a withdrawal the account no longer exists, so its query is removed (a refetch would show
 * the 404) and the page is replaced by the tab Chờ duyệt, where the toast shows. Its role is `status`, not shadcn's
 * `alert`, so that a notice shown on every load is not announced assertively. Nothing unless the account is PENDING.
 */
export function StaffAccountNoticeSection() {
  const { account, etag } = useStaffAccountState()
  return account?.status === 'PENDING' ? <PendingNotice account={account} etag={etag} /> : null
}

/** The notice itself, mounted only for a PENDING account, so that its commands load nowhere else. */
function PendingNotice({ account, etag }: { account: StaffDto; etag: string | undefined }) {
  const t = useTranslations('staff.account')
  const dateTime = useDateTime()
  const client = useQueryClient()
  const router = useRouter()
  const approval = useStaffApproval((_action, target) => {
    client.removeQueries({ queryKey: getGetApiV1AdminStaffIdQueryKey(target.id) })
    router.replace(PENDING_TAB)
  })
  const [action, setAction] = useState<ApprovalAction | null>(null)
  const [open, setOpen] = useState(false)
  const opener = useRef<HTMLButtonElement | null>(null)

  function choose(chosen: ApprovalAction, button: HTMLButtonElement) {
    opener.current = button
    setAction(chosen)
    setOpen(true)
  }
  let other = t('pendingNotice.other')
  if (account.canApprove) other = t('pendingNotice.review')
  else if (account.canWithdraw) other = t('pendingNotice.own')

  return (
    <Alert role="status" className="md:flex md:items-center md:gap-4">
      <ClockIcon aria-hidden />
      <div className="min-w-0 flex-1">
        <AlertTitle>{t('pendingNotice.title')}</AlertTitle>
        <AlertDescription>
          <p>
            {account.createdBy && account.createdAt
              ? `${t('pendingNotice.createdBy', { name: account.createdBy.name, time: dateTime(account.createdAt) })} `
              : ''}
            {t('status.pendingDescription')} {other}
          </p>
        </AlertDescription>
      </div>
      {account.canApprove && (
        <div className="col-start-2 mt-3 grid grid-cols-2 gap-2 md:mt-0 md:flex">
          <Button
            variant="destructive-soft"
            onClick={(event) => choose('reject', event.currentTarget)}
          >
            {t('reject')}
          </Button>
          <Button onClick={(event) => choose('approve', event.currentTarget)}>
            <CheckIcon aria-hidden />
            {t('approve')}
          </Button>
        </div>
      )}
      {!account.canApprove && account.canWithdraw && (
        <Button
          variant="destructive-soft"
          className="col-start-2 mt-3 w-full md:mt-0 md:w-auto"
          onClick={(event) => choose('withdraw', event.currentTarget)}
        >
          <Undo2Icon aria-hidden />
          {t('withdraw')}
        </Button>
      )}
      {action && (
        <StaffApprovalDialog
          action={action}
          account={account}
          open={open}
          pending={approval.pending}
          onOpenChange={setOpen}
          onConfirm={() =>
            approval.run(action, { id: account.id, name: account.name, version: etag })
          }
          onClosed={() => opener.current?.focus()}
        />
      )}
    </Alert>
  )
}
