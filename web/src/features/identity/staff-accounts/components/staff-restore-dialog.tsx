'use client'

import { LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { toast } from 'sonner'
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
import { sessionOutcome } from '@/features/identity/access'
import { useProblemToast } from '@/hooks/use-problem-toast'
import type { StaffDto } from '@/lib/api/generated/model'
import { useRestoreStaff } from '../hooks/use-staff-status'

/**
 * The confirmation "Khôi phục tài khoản?" of SCR-STF-03 (STF-02; R-USR-03, R-USR-12), which a row of SCR-STF-01 opens
 * too. "Khôi phục" is a Button, not AlertDialogAction, which would close before the answer comes; it closes on 200,
 * and on any problem after its toast (the answers about the session go to the admin layout's rules).
 */
export function StaffRestoreDialog({
  account,
  open,
  onOpenChange,
}: {
  account: Pick<StaffDto, 'id' | 'name'>
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const t = useTranslations('staff.account')
  const restore = useRestoreStaff(account.id)
  const showProblem = useProblemToast()

  async function confirm() {
    try {
      await restore.mutateAsync({ id: account.id })
      toast.success(t('restored'))
    } catch (error) {
      if (!sessionOutcome(error)) showProblem(error)
    }
    onOpenChange(false)
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t('restoreDialog.title')}</AlertDialogTitle>
          <AlertDialogDescription>{t('restoreDialog.description')}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t('cancel')}</AlertDialogCancel>
          <Button onClick={() => void confirm()} disabled={restore.isPending}>
            {restore.isPending && (
              <LoaderCircleIcon className="animate-spin" data-icon="inline-start" aria-hidden />
            )}
            {t('restoreDialog.submit')}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
