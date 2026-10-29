'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { useMemo, useRef, useState, type BaseSyntheticEvent } from 'react'
import { useForm } from 'react-hook-form'
import type { z } from 'zod'
import { NO_FORM_PROBLEMS } from '@/components/app/form-problems'
import { sessionOutcome } from '@/features/identity/access'
import { useProblemToast } from '@/hooks/use-problem-toast'
import { ApiError, fieldErrors, hasProblemType } from '@/lib/api/problems'
import { changeMyPassword } from '../api/me'
import { passwordChangeSchema } from '../schemas'

/** The key of the change while it is sent, which keeps the dialog of SCR-ACC-01 open. */
export const changePasswordKey = ['me', 'change-password'] as const

/**
 * The password change of SCR-AUT-02 (docs/SCREENS.md): the new password typed twice, sent to POST
 * /me/change-password (DEC-094), and the sign-in again of 403 recent-login-required. After a 204 every session is
 * revoked, so `onChanged` signs the user out.
 */
export function useChangePassword(onChanged: () => Promise<void>) {
  const t = useTranslations()
  const showProblem = useProblemToast()
  const [reauthOpen, setReauthOpen] = useState(false)
  // Read when Firebase answers: "Hủy" or Escape while it checks the password closes the dialog at once, during its
  // exit animation too, and cancels the change.
  const reauthShown = useRef(false)
  function showReauth(open: boolean) {
    reauthShown.current = open
    setReauthOpen(open)
  }
  // From the 204 until the page changes: the button stays disabled.
  const [leaving, setLeaving] = useState(false)
  // The texts of a 400 that no field shows, until the next send.
  const [problems, setProblems] = useState(NO_FORM_PROBLEMS)

  const schema = useMemo(
    () =>
      passwordChangeSchema({
        required: t('changePassword.validation.required'),
        length: t('changePassword.validation.length'),
        mismatch: t('changePassword.validation.mismatch'),
      }),
    [t],
  )
  const form = useForm<z.input<typeof schema>, unknown, z.output<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { newPassword: '', confirmation: '' },
  })
  // The password is read from the form, not passed as the variables of the mutation, which the mutation cache would
  // keep after the request (SCR-AUT-02: the password is never kept).
  const change = useMutation({
    mutationKey: changePasswordKey,
    mutationFn: () => changeMyPassword(form.getValues('newPassword')),
  })

  // `retry`: the send that follows the sign-in again of 403 recent-login-required, which happens once.
  async function send(retry: boolean) {
    setProblems(NO_FORM_PROBLEMS)
    try {
      await change.mutateAsync()
    } catch (error) {
      showFailure(error, retry)
      return
    }
    setLeaving(true)
    // The passwords leave the form state at once.
    form.reset()
    await onChanged()
  }

  function showFailure(error: unknown, retry: boolean) {
    // 401 and the session problems: the admin layout follows them (use-staff-session.tsx).
    if (sessionOutcome(error)) return
    if (error instanceof ApiError) {
      // The API's texts (DEC-117 (4)): under the field, and those no field shows at the top of the form.
      if (error.status === 400 && error.problem?.errors?.length) {
        const { byField, other } = fieldErrors(error.problem, ['newPassword'])
        if (byField.newPassword) {
          form.setError('newPassword', { message: byField.newPassword }, { shouldFocus: true })
        }
        setProblems({ texts: other, focus: !byField.newPassword })
        return
      }
      // The sign-in again happens once: a second 403 shows its text.
      if (
        error.status === 403 &&
        hasProblemType(error.problem, 'recent-login-required') &&
        !retry
      ) {
        showReauth(true)
        return
      }
    }
    showProblem(error)
  }

  function onConfirmed() {
    if (!reauthShown.current) return
    showReauth(false)
    void send(true)
  }

  return {
    control: form.control,
    problems,
    sending: form.formState.isSubmitting || change.isPending || leaving,
    onSubmit: (event: BaseSyntheticEvent) => void form.handleSubmit(() => send(false))(event),
    reauth: {
      open: reauthOpen,
      onOpenChange: showReauth,
      onConfirmed,
      onClosed: () => form.setFocus('newPassword'),
    },
  }
}
