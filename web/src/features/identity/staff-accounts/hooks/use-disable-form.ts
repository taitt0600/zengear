'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslations } from 'next-intl'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { NO_FORM_PROBLEMS } from '@/components/app/form-problems'
import { sessionOutcome } from '@/features/identity/access'
import { useProblemToast } from '@/hooks/use-problem-toast'
import type { StaffDto } from '@/lib/api/generated/model'
import { ApiError, fieldErrors } from '@/lib/api/problems'
import { staffDisableSchema, type StaffDisableValues } from '../schemas'
import { todayInVietnam } from '../utils/staff-account'
import { useDisableStaff } from './use-staff-status'

/** What the dialog "Khóa tài khoản" needs of the account: SCR-STF-03 and a row of SCR-STF-01 both give it. */
export type DisableAccount = Pick<StaffDto, 'id' | 'name' | 'joinedDate' | 'leftDate'>

/**
 * The form of the dialog "Khóa tài khoản" (SCR-STF-03): an account that has a left date opens ticked with it, any
 * other unticked; ticking picks today in Vietnam. It sends `{ leftDate }`, null when not ticked. A 400, or a 409 about
 * the left date, stays in the dialog (under the field, or at its top); every other problem goes to a toast and `onDone`
 * closes the dialog, and the answers about the session go to the admin layout's rules (docs/SCREENS.md, Conventions).
 */
export function useDisableForm(account: DisableAccount, onDone: () => void) {
  const t = useTranslations('staff.account')
  const showProblem = useProblemToast()
  const disable = useDisableStaff(account.id)
  const [problems, setProblems] = useState(NO_FORM_PROBLEMS)
  const today = todayInVietnam()
  const form = useForm<StaffDisableValues>({
    resolver: zodResolver(staffDisableSchema),
    defaultValues: { leaving: account.leftDate !== null, leftDate: account.leftDate },
  })

  function setLeaving(leaving: boolean) {
    form.setValue('leaving', leaving)
    form.setValue('leftDate', leaving ? today : null)
    form.clearErrors('leftDate')
  }

  function showFailure(error: unknown) {
    if (sessionOutcome(error)) return
    const items = error instanceof ApiError ? (error.problem?.errors ?? []) : []
    if (error instanceof ApiError && items.length > 0 && [400, 409].includes(error.status)) {
      const { byField, other } = fieldErrors(error.problem, ['leftDate'])
      // Under the date while it shows (`StaffLeftDateField` then takes the focus: the date is not an input React Hook
      // Form registers, so `shouldFocus` would reach nothing); at the top while "Nhân viên nghỉ việc" is not ticked.
      const underField =
        byField.leftDate && form.getValues('leaving') ? byField.leftDate : undefined
      if (underField) form.setError('leftDate', { message: underField })
      const top = byField.leftDate && !underField ? [byField.leftDate, ...other] : other
      setProblems({ texts: top, focus: !underField })
      return
    }
    showProblem(error)
    onDone()
  }

  const submit = form.handleSubmit(async ({ leaving, leftDate }) => {
    setProblems(NO_FORM_PROBLEMS)
    try {
      await disable.mutateAsync({ id: account.id, data: { leftDate: leaving ? leftDate : null } })
    } catch (error) {
      showFailure(error)
      return
    }
    toast.success(t('disabled'))
    onDone()
  })

  return {
    form,
    submit,
    problems,
    setLeaving,
    today,
    // A joined date after today leaves no day to pick (R-USR-07).
    joinsLater: account.joinedDate !== null && account.joinedDate > today,
    pending: form.formState.isSubmitting,
  }
}
