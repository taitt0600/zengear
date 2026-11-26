'use client'

import { useQueryClient } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { useRouter } from 'next/navigation'
import { toast } from 'sonner'
import { sessionOutcome } from '@/features/identity/access'
import { useProblemToast } from '@/hooks/use-problem-toast'
import {
  usePostApiV1AdminStaffIdApprove,
  usePostApiV1AdminStaffIdReject,
  usePostApiV1AdminStaffIdWithdraw,
} from '@/lib/api/generated/staff-approval/staff-approval'
import {
  getGetApiV1AdminStaffIdQueryKey,
  getGetApiV1AdminStaffQueryKey,
} from '@/lib/api/generated/staff/staff'
import { newStaffPath } from '../utils/paths'
import { useCanCreateStaff } from './use-can-create-staff'

/** The three commands on a PENDING account (STF-09, UC-140). */
export type ApprovalAction = 'approve' | 'reject' | 'withdraw'

/** The account a command acts on: its id, its name for the toast, and the version it was loaded at (DEC-140 (2)). */
export type ApprovalTarget = { id: string; name: string; version: string | undefined }

/**
 * POST /api/v1/admin/staff/{id}/approve, /reject and /withdraw, shared by the tab Chờ duyệt of SCR-STF-01 and the
 * notice of SCR-STF-03. Each sends the version the user saw as `If-Match` (the item's `version`, or the account's
 * `ETag`), so data changed since gets 412. After an answer, whatever it is, the staff list (and with it the count of
 * the tab and of the menu) and the account load again (docs/FRONTEND.md: invalidate, never edit the cache by hand);
 * a refusal shows the problem's `detail` in a toast, an answer about the session is left to the admin layout's rules.
 * `onGone` runs after a 204 of reject or withdraw, when the account no longer exists (SCR-STF-03 leaves its page).
 */
export function useStaffApproval(
  onGone?: (action: ApprovalAction, target: ApprovalTarget) => void,
) {
  const t = useTranslations('staff.account')
  const client = useQueryClient()
  const router = useRouter()
  const canCreate = useCanCreateStaff()
  const showProblem = useProblemToast()
  const approve = usePostApiV1AdminStaffIdApprove()
  const reject = usePostApiV1AdminStaffIdReject()
  const withdraw = usePostApiV1AdminStaffIdWithdraw()
  const mutations = { approve, reject, withdraw }

  const reloadList = () => client.invalidateQueries({ queryKey: getGetApiV1AdminStaffQueryKey() })

  function done(action: ApprovalAction, target: ApprovalTarget) {
    if (action === 'approve') {
      toast.success(t('approved', { name: target.name }), { description: t('approvedHint') })
      void client.invalidateQueries({ queryKey: getGetApiV1AdminStaffIdQueryKey(target.id) })
      return
    }
    if (action === 'reject') toast.success(t('rejected', { name: target.name }))
    else {
      toast.success(t('withdrawn', { name: target.name }), {
        action: canCreate
          ? { label: t('withdrawnCreate'), onClick: () => router.push(newStaffPath) }
          : undefined,
      })
    }
    onGone?.(action, target)
  }

  /** Sends the command; resolves once its answer is handled, so the dialog then closes. */
  async function run(action: ApprovalAction, target: ApprovalTarget) {
    const headers = target.version ? { 'If-Match': target.version } : undefined
    try {
      await mutations[action].mutateAsync({ id: target.id, headers })
      done(action, target)
    } catch (error) {
      if (sessionOutcome(error)) return
      showProblem(error)
      void client.invalidateQueries({ queryKey: getGetApiV1AdminStaffIdQueryKey(target.id) })
    } finally {
      void reloadList()
    }
  }

  return { run, pending: approve.isPending || reject.isPending || withdraw.isPending }
}
