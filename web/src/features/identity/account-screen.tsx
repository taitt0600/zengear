'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useIsMutating, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId, useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import type { z } from 'zod'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError, fieldErrors } from '@/lib/api/problems'
import { accountSchema } from './account-schemas'
import { ChangePasswordForm, changePasswordKey } from './change-password-form'
import { meQuery, updateMe, type LoadedCurrentUser } from './me'
import { sessionOutcome } from './me-outcome'
import { useProblemToast } from './problem-toast'
import { PASSWORD_CHANGED } from './routes'
import { useStaffSignOut } from './staff-session'

/**
 * SCR-ACC-01 My account (UC-007, STF-03): the display name, the email read-only, and the password change of
 * SCR-AUT-02 in a dialog. Loaded from GET /me, the query the admin layout already holds.
 */
export function AccountScreen() {
  const t = useTranslations()
  const me = useQuery(meQuery)
  return (
    <div className="flex max-w-lg flex-col gap-6">
      <h1 className="text-xl font-semibold">{t('admin.user.account')}</h1>
      {me.data ? <AccountForm loaded={me.data} /> : <AccountSkeleton />}
      <Separator />
      <ChangePasswordDialog email={me.data?.email} />
    </div>
  )
}

/**
 * The display name, sent with the ETag of the values the form loaded (docs/SCREENS.md, Conventions, Edits with an
 * ETag): a background refetch of GET /me changes neither the field nor that version, so "Lưu" never overwrites a
 * change the form did not show. The form loads again after a 200, a 412 or a 428.
 */
function AccountForm({ loaded }: { loaded: LoadedCurrentUser }) {
  const t = useTranslations()
  const id = useId()
  const queryClient = useQueryClient()
  const showProblem = useProblemToast()
  const [etag, setEtag] = useState(loaded.etag)
  const save = useMutation({ mutationFn: (name: string) => updateMe(name, etag ?? '') })

  const schema = useMemo(
    () =>
      accountSchema({
        required: t('account.validation.nameRequired'),
        tooLong: t('account.validation.nameTooLong'),
      }),
    [t],
  )
  const form = useForm<z.input<typeof schema>, unknown, z.output<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { name: loaded.name },
  })

  function hold(account: LoadedCurrentUser) {
    form.reset({ name: account.name })
    setEtag(account.etag)
  }

  // GET /me again (the header shows the new name from the same query), then the form holds that version. When it
  // fails, the form keeps what was typed and the failure is shown, instead of the version it already had.
  async function reload() {
    try {
      hold(await queryClient.query({ ...meQuery, staleTime: 0 }))
    } catch (error) {
      // A session answer: the admin layout is already signing the user out.
      if (!sessionOutcome(error)) showProblem(error)
    }
  }

  async function onSubmit({ name }: z.output<typeof schema>) {
    let saved: LoadedCurrentUser
    try {
      saved = await save.mutateAsync(name)
    } catch (error) {
      await showFailure(error)
      return
    }
    // The form holds the answer of PATCH, which a failed GET /me afterwards cannot undo; the header follows GET /me.
    hold(saved)
    void queryClient.invalidateQueries({ queryKey: meQuery.queryKey })
    toast.success(t('account.saved'))
  }

  async function showFailure(error: unknown) {
    // 401 and the session problems: the admin layout follows them (staff-session.tsx).
    if (sessionOutcome(error)) return
    if (error instanceof ApiError) {
      // The API's messages are English: the field shows the web's own text (owner's answer, docs/SCREENS.md).
      if (error.status === 400 && fieldErrors(error.problem).name) {
        form.setError(
          'name',
          { message: t('account.validation.nameRefused') },
          { shouldFocus: true },
        )
        return
      }
      // 428 means the version was missing: reloading gets one, as for a 412.
      if (error.status === 412 || error.status === 428) {
        toast.error(t('admin.stale'))
        await reload()
        return
      }
    }
    showProblem(error)
  }

  const sending = form.formState.isSubmitting
  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <Controller
          name="name"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={`${id}-name`}>{t('account.name')}</FieldLabel>
              <Input
                {...field}
                id={`${id}-name`}
                autoComplete="name"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
        <Field>
          <FieldLabel htmlFor={`${id}-email`}>{t('account.email')}</FieldLabel>
          <Input
            id={`${id}-email`}
            type="email"
            value={loaded.email ?? ''}
            readOnly
            aria-describedby={`${id}-email-note`}
          />
          <FieldDescription id={`${id}-email-note`}>{t('account.emailNote')}</FieldDescription>
        </Field>
        <div>
          {/* Without a version nothing can be saved: the API would answer 428. */}
          <Button type="submit" disabled={sending || !etag} aria-busy={sending}>
            {sending && (
              <LoaderCircleIcon data-icon="inline-start" className="motion-safe:animate-spin" />
            )}
            {t('account.save')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

/**
 * "Đổi mật khẩu": the form of SCR-AUT-02 in a dialog; after the change the user is signed out (DEC-094). It stays
 * open while the change is sent, so that its answer is shown.
 */
function ChangePasswordDialog({ email }: { email: string | null | undefined }) {
  const t = useTranslations()
  const signOut = useStaffSignOut()
  const [open, setOpen] = useState(false)
  const changing = useIsMutating({ mutationKey: changePasswordKey }) > 0
  return (
    <Dialog open={open} onOpenChange={(next) => setOpen(next || changing)}>
      <DialogTrigger asChild>
        <Button variant="outline" className="self-start">
          {t('account.changePassword')}
        </Button>
      </DialogTrigger>
      {/* The form's own "Hủy" closes the dialog, so the corner close button is off. Taller
          than a short screen, the dialog scrolls inside instead of losing its top and its buttons. */}
      <DialogContent
        showCloseButton={false}
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto overscroll-contain"
      >
        <DialogHeader>
          <DialogTitle>{t('changePassword.title')}</DialogTitle>
          <DialogDescription>{t('account.changePasswordNote')}</DialogDescription>
        </DialogHeader>
        <ChangePasswordForm
          email={email}
          onCancel={() => setOpen(false)}
          onChanged={() => signOut(PASSWORD_CHANGED)}
        />
      </DialogContent>
    </Dialog>
  )
}

/** The form while GET /me loads: its fields in place, never an empty screen. */
function AccountSkeleton() {
  return (
    <FieldGroup>
      {[0, 1].map((field) => (
        <Field key={field}>
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-9 w-full" />
        </Field>
      ))}
      <Skeleton className="h-9 w-16" />
    </FieldGroup>
  )
}
