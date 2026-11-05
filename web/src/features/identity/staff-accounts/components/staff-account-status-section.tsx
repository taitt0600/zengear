'use client'

import { LockIcon, RotateCcwIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId, useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import type { StaffDto } from '@/lib/api/generated/model'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useStaffStatusTexts } from '../hooks/use-staff-texts'
import { StaffDisableDialog } from './staff-disable-dialog'
import { StaffRestoreDialog } from './staff-restore-dialog'
import { StaffStatusBadge } from './staff-status-badge'

/**
 * The card "Trạng thái tài khoản" of SCR-STF-03 (STF-02): the status, the unchanged default password, and, for a user
 * who holds `staff.disable`, the way to lock or restore the account, never on their own (R-USR-12) nor on an
 * anonymized one (R-USR-03). Hiding only: the API decides. Nothing when the account cannot show.
 */
export function StaffAccountStatusSection() {
  const t = useTranslations()
  const statusTexts = useStaffStatusTexts()
  const { account, error, canDisable, isOwn } = useStaffAccountState()
  if (error) return null

  return (
    <Card aria-busy={account ? undefined : true}>
      <CardHeader>
        {account ? (
          <>
            <CardTitle>
              <h2>{t('staff.account.status.title')}</h2>
            </CardTitle>
            <CardDescription>{t('staff.account.status.description')}</CardDescription>
          </>
        ) : (
          <>
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-4 w-full" />
          </>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {account ? (
          <>
            <div className="flex items-center justify-between gap-2">
              <span className="text-sm text-muted-foreground">
                {t('staff.list.columns.status')}
              </span>
              <StaffStatusBadge status={account.status} texts={statusTexts} />
            </div>
            {account.mustChangePassword && (
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm text-muted-foreground">{t('staff.account.password')}</span>
                <Badge variant="outline">{t('staff.account.mustChangePassword')}</Badge>
              </div>
            )}
            {canDisable && <StatusActions account={account} isOwn={isOwn} />}
          </>
        ) : (
          <Skeleton className="h-9 w-full rounded-md" />
        )}
      </CardContent>
    </Card>
  )
}

function StatusActions({ account, isOwn }: { account: StaffDto; isOwn: boolean }) {
  const t = useTranslations('staff.account')
  // The id of the page's address: the commands reload the account under the key the page loaded it with, which a
  // GUID typed in capitals would not match with the API's own `account.id`.
  const { id } = useStaffAccountState()
  const target = { ...account, id }
  const [dialog, setDialog] = useState<'disable' | 'restore' | null>(null)
  const hintId = useId()
  const toggle = (name: 'disable' | 'restore') => (open: boolean) => setDialog(open ? name : null)

  if (isOwn || account.status === 'ANONYMIZED') {
    return (
      <p className="text-sm text-muted-foreground">{isOwn ? t('ownAccount') : t('anonymized')}</p>
    )
  }
  const lock = (label: string, describedBy?: string) => (
    <Button
      variant="destructive-soft"
      className="w-full"
      aria-describedby={describedBy}
      onClick={() => setDialog('disable')}
    >
      <LockIcon data-icon="inline-start" aria-hidden />
      {label}
    </Button>
  )

  return (
    <>
      {account.status === 'ACTIVE' ? (
        lock(t('disable'))
      ) : (
        <div className="flex flex-col gap-2">
          <Button className="w-full" onClick={() => setDialog('restore')}>
            <RotateCcwIcon data-icon="inline-start" aria-hidden />
            {t('restore')}
          </Button>
          {lock(t('disableAgain'), hintId)}
          <p id={hintId} className="text-sm text-muted-foreground">
            {t('disableAgainHint')}
          </p>
        </div>
      )}
      <StaffDisableDialog
        account={target}
        open={dialog === 'disable'}
        onOpenChange={toggle('disable')}
      />
      <StaffRestoreDialog
        account={target}
        open={dialog === 'restore'}
        onOpenChange={toggle('restore')}
      />
    </>
  )
}
