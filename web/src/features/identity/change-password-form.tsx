import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId, useMemo, useRef, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import type { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { ApiError, fieldErrors, problemName } from '@/lib/api/problems'
import { passwordChangeSchema } from './account-schemas'
import { changeMyPassword } from './me'
import { sessionOutcome } from './me-outcome'
import { useProblemToast } from './problem-toast'
import { AccountName } from './account-name'
import { ReauthDialog } from './reauth-dialog'

/** The key of the change while it is sent, which keeps the dialog of SCR-ACC-01 open. */
export const changePasswordKey = ['me', 'change-password'] as const

/**
 * The form of SCR-AUT-02 (docs/SCREENS.md), also opened in a dialog by SCR-ACC-01 with the same answers: the new
 * password typed twice, sent to POST /me/change-password (DEC-094). After a 204 every session is revoked, so
 * `onChanged` signs the user out; `onCancel`, given only in a dialog, closes it. `email` names the account to password
 * managers in a field the user does not see.
 */
export function ChangePasswordForm({
  email,
  onChanged,
  onCancel,
}: {
  email: string | null | undefined
  onChanged: () => Promise<void>
  onCancel?: () => void
}) {
  const t = useTranslations()
  const id = useId()
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
    // 401 and the session problems: the admin layout follows them (staff-session.tsx).
    if (sessionOutcome(error)) return
    if (error instanceof ApiError) {
      // The API's messages are English: the field shows the web's own text (owner's answer, docs/SCREENS.md).
      if (error.status === 400 && fieldErrors(error.problem).newPassword) {
        form.setError(
          'newPassword',
          { message: t('changePassword.validation.refused') },
          { shouldFocus: true },
        )
        return
      }
      if (error.status === 403 && problemName(error.problem) === 'recent-login-required') {
        if (retry) toast.error(t('problems.unknown'))
        else showReauth(true)
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

  const sending = form.formState.isSubmitting || change.isPending || leaving

  return (
    <>
      <form noValidate onSubmit={(event) => void form.handleSubmit(() => send(false))(event)}>
        <FieldGroup>
          {email && <AccountName email={email} />}
          <Controller
            name="newPassword"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor={`${id}-new`}>{t('changePassword.newPassword')}</FieldLabel>
                <Input
                  {...field}
                  id={`${id}-new`}
                  type="password"
                  autoComplete="new-password"
                  aria-describedby={`${id}-help`}
                  aria-invalid={fieldState.invalid}
                />
                <FieldDescription id={`${id}-help`}>{t('changePassword.help')}</FieldDescription>
                {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
              </Field>
            )}
          />
          <Controller
            name="confirmation"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor={`${id}-confirmation`}>
                  {t('changePassword.confirmation')}
                </FieldLabel>
                <Input
                  {...field}
                  id={`${id}-confirmation`}
                  type="password"
                  autoComplete="new-password"
                  aria-invalid={fieldState.invalid}
                />
                {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
              </Field>
            )}
          />
          <div className="flex justify-end gap-2">
            {onCancel && (
              <Button type="button" variant="outline" onClick={onCancel} disabled={sending}>
                {t('changePassword.cancel')}
              </Button>
            )}
            <Button type="submit" disabled={sending} aria-busy={sending}>
              {sending && (
                <LoaderCircleIcon data-icon="inline-start" className="motion-safe:animate-spin" />
              )}
              {t('changePassword.submit')}
            </Button>
          </div>
        </FieldGroup>
      </form>
      <ReauthDialog
        email={email}
        open={reauthOpen}
        onOpenChange={showReauth}
        onConfirmed={onConfirmed}
        onClosed={() => form.setFocus('newPassword')}
      />
    </>
  )
}
