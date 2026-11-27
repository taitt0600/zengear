'use client'

import { LockIcon, RotateCcwIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useEffect, useId, useRef, useState, type MouseEvent, type RefObject } from 'react'
import { Button } from '@/components/ui/button'
import type { StaffDto } from '@/lib/api/generated/model'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useStaffStatusTexts } from '../hooks/use-staff-texts'
import { StaffAccountRow } from './staff-account-row'
import { StaffDisableDialog } from './staff-disable-dialog'
import { StaffRestoreDialog } from './staff-restore-dialog'
import { StaffStatusBadge } from './staff-status-badge'

type Command = 'disable' | 'restore'

/**
 * The row "Trạng thái" of the card "Tài khoản" of SCR-STF-03 (STF-02): the status and what it means, and, for a user
 * who holds `staff.disable`, the actions at the end of the row: "Khóa tài khoản" on an ACTIVE account; "Khôi phục tài
 * khoản" then "Khóa lại" on a DISABLED one (a lock sent again after a 500 revokes the sessions again); none on the
 * user's own account (R-USR-12) nor on an anonymized one (R-USR-03), which say why; none on a PENDING account, whose
 * notice holds the approval (STF-09, R-USR-17). Hiding only: the API decides. `heading` is the card's h2, which takes
 * the focus when a dialog closes after a 200 replaced the button that opened it.
 */
export function StaffStatusRow({
  account,
  heading,
}: {
  account: StaffDto
  heading: RefObject<HTMLHeadingElement | null>
}) {
  const t = useTranslations('staff')
  const statusTexts = useStaffStatusTexts()
  // The id of the page's address: the commands reload the account under the key the page loaded it with, which a
  // GUID typed in capitals would not match with the API's own `account.id`.
  const { id, canDisable, isOwn } = useStaffAccountState()
  const hintId = useId()
  const againId = useId()
  const [dialog, setDialog] = useState<Command | null>(null)
  // The dialogs open without a trigger, so they put the focus back themselves: on the button that opened them, or,
  // once a 200 has replaced it (the account reloads before they close), on the card's heading.
  const opener = useRef<HTMLButtonElement | null>(null)
  // A reload that ends after the dialog closed (after a refusal, or "Hủy" while the command runs) can still replace
  // the button that got the focus back: the heading takes it instead of the page's body.
  useEffect(() => {
    if (opener.current?.isConnected === false && document.activeElement === document.body) {
      heading.current?.focus()
      opener.current = null
    }
  }, [account.status, heading])

  const { status } = account
  const lockable = status === 'ACTIVE' || status === 'DISABLED'
  const actionable = canDisable && !isOwn && lockable
  const openWith = (name: Command, event: MouseEvent<HTMLButtonElement>) => {
    opener.current = event.currentTarget
    setDialog(name)
  }
  const toggle = (name: Command) => (open: boolean) => setDialog(open ? name : null)
  const focusBack = () => (opener.current?.isConnected ? opener.current : heading.current)?.focus()
  // Each button keyed by what it does: a lock replaces "Khóa tài khoản" with "Khôi phục tài khoản", which must be a new
  // element, not the same one relabelled, so that the focus goes to the heading, not to a button that did not open.
  const lock = (label: string, describedBy: string, key: string) => (
    <Button
      key={key}
      variant="destructive-soft"
      aria-describedby={describedBy}
      onClick={(event) => openWith('disable', event)}
    >
      <LockIcon data-icon="inline-start" aria-hidden />
      {label}
    </Button>
  )
  const muted = (text: string, textId?: string) => (
    <span id={textId} className="text-muted-foreground">
      {text}
    </span>
  )

  return (
    <>
      <StaffAccountRow
        label={t('list.columns.status')}
        action={
          actionable &&
          (status === 'ACTIVE'
            ? [lock(t('account.disable'), hintId, 'disable')]
            : [
                <Button key="restore" onClick={(event) => openWith('restore', event)}>
                  <RotateCcwIcon data-icon="inline-start" aria-hidden />
                  {t('account.restore')}
                </Button>,
                lock(t('account.disableAgain'), againId, 'disable-again'),
              ])
        }
      >
        <div className="flex flex-col items-start gap-1.5">
          <StaffStatusBadge status={status} texts={statusTexts} />
          {status === 'PENDING' && muted(t('account.status.pendingDescription'))}
          {lockable && muted(t('account.status.description'), hintId)}
          {actionable && status === 'DISABLED' && muted(t('account.disableAgainHint'), againId)}
          {canDisable && isOwn && muted(t('account.ownAccount'))}
          {canDisable && status === 'ANONYMIZED' && muted(t('account.anonymized'))}
        </div>
      </StaffAccountRow>
      {actionable && (
        <>
          <StaffDisableDialog
            account={{ ...account, id }}
            open={dialog === 'disable'}
            onOpenChange={toggle('disable')}
            onClosed={focusBack}
          />
          <StaffRestoreDialog
            account={{ ...account, id }}
            open={dialog === 'restore'}
            onOpenChange={toggle('restore')}
            onClosed={focusBack}
          />
        </>
      )}
    </>
  )
}
