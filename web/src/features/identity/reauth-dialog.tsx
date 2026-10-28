import { zodResolver } from '@hookform/resolvers/zod'
import { LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId, useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { reauthenticate } from '@/lib/auth/session'
import { AccountName } from './account-name'
import { firebaseError, type PasswordSignInError } from './me-outcome'

/**
 * The sign-in again of SCR-AUT-02 for 403 recent-login-required (DEC-094): the current password, checked by Firebase.
 * `onConfirmed` runs once Firebase accepted it; `onClosed` puts the focus back, since the button that opened it is
 * disabled while its request runs. The form lives inside the dialog's content, which unmounts when the
 * dialog closes, so the password typed here never outlives it.
 */
export function ReauthDialog({
  email,
  open,
  onOpenChange,
  onConfirmed,
  onClosed,
}: {
  email: string | null | undefined
  open: boolean
  onOpenChange: (open: boolean) => void
  onConfirmed: () => void
  onClosed: () => void
}) {
  const t = useTranslations('changePassword')
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {/* "Hủy" below closes the dialog, so the corner close button is off. Taller than a
          short screen, the dialog scrolls inside instead of losing its top and its buttons. */}
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto overscroll-contain"
        onCloseAutoFocus={(event) => {
          event.preventDefault()
          onClosed()
        }}
      >
        <DialogHeader>
          <DialogTitle>{t('reauth.title')}</DialogTitle>
          <DialogDescription>{t('reauth.description')}</DialogDescription>
        </DialogHeader>
        <ReauthForm email={email} onConfirmed={onConfirmed} />
      </DialogContent>
    </Dialog>
  )
}

function ReauthForm({
  email,
  onConfirmed,
}: {
  email: string | null | undefined
  onConfirmed: () => void
}) {
  const t = useTranslations()
  const id = useId()
  const [error, setError] = useState<PasswordSignInError | null>(null)
  const schema = useMemo(
    () =>
      z.object({
        password: z.string().min(1, { error: t('changePassword.reauth.required') }),
      }),
    [t],
  )
  const form = useForm<z.input<typeof schema>, unknown, z.output<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { password: '' },
  })

  async function onSubmit({ password }: z.output<typeof schema>) {
    setError(null)
    try {
      await reauthenticate(password)
    } catch (failure) {
      setError(firebaseError(failure))
      return
    }
    onConfirmed()
  }

  const sending = form.formState.isSubmitting
  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        {email && <AccountName email={email} />}
        {error && (
          <Alert variant="destructive">
            <AlertDescription>
              {error === 'signIn.errors.wrongCredentials'
                ? t('changePassword.reauth.wrongPassword')
                : t(error)}
            </AlertDescription>
          </Alert>
        )}
        <Controller
          name="password"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={`${id}-password`}>
                {t('changePassword.reauth.password')}
              </FieldLabel>
              <Input
                {...field}
                id={`${id}-password`}
                type="password"
                autoComplete="current-password"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
        <DialogFooter>
          <DialogClose asChild>
            <Button type="button" variant="outline">
              {t('changePassword.cancel')}
            </Button>
          </DialogClose>
          <Button type="submit" disabled={sending} aria-busy={sending}>
            {sending && (
              <LoaderCircleIcon data-icon="inline-start" className="motion-safe:animate-spin" />
            )}
            {t('changePassword.reauth.submit')}
          </Button>
        </DialogFooter>
      </FieldGroup>
    </form>
  )
}
