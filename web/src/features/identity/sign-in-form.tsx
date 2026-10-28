'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { EyeIcon, EyeOffIcon, LoaderCircleIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from '@/components/ui/input-group'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError, isNetworkError } from '@/lib/api/problems'
import { hasSession, signInWithPassword, signOutAndClear } from '@/lib/auth/session'
import { meQuery } from './me'
import {
  firebaseError,
  meOutcome,
  SIGN_OUT_REASONS,
  type MeOutcome,
  type SignOutReason,
} from './me-outcome'
import { useProblemText } from './problem-toast'
import { adminRoutes, PASSWORD_CHANGED, safeNext } from './routes'
import { takeSignOutReason } from './sign-out-reason'

/** The reasons of a sign-out that the web words itself, as keys of `signIn.errors` (SCR-AUT-01). */
const REASON_ERRORS = {
  'account-disabled': 'accountDisabled',
  'not-staff': 'notStaff',
} as const satisfies Record<SignOutReason, string>

function reasonOf(reason: string | null): SignOutReason | undefined {
  return SIGN_OUT_REASONS.find((value) => value === reason)
}

/** SCR-AUT-01: staff sign in with their company email and password (UC-001, AUT-01). */
export function SignInForm() {
  const t = useTranslations('signIn')
  const tRoot = useTranslations()
  const problemText = useProblemText()
  const router = useRouter()
  const params = useSearchParams()
  const queryClient = useQueryClient()
  // The text of the alert: the web's own or the API's (DEC-117 (4)).
  const [error, setError] = useState<string | null>(() => {
    const reason = reasonOf(params.get('reason'))
    return reason ? t(`errors.${REASON_ERRORS[reason]}`) : null
  })
  // Shown until the next sign-in attempt.
  const [passwordChanged, setPasswordChanged] = useState(
    () => params.get('reason') === PASSWORD_CHANGED,
  )
  const [showPassword, setShowPassword] = useState(false)
  // Until the SDK has read its session, and from the moment the user is signed in until the next page opens: the
  // button stays disabled, so a second click sends nothing.
  const [sessionRead, setSessionRead] = useState(false)
  const [leaving, setLeaving] = useState(false)

  const schema = useMemo(
    () =>
      z.object({
        email: z
          .string()
          .trim()
          .min(1, { error: t('validation.emailRequired') })
          .pipe(z.email({ error: t('validation.emailInvalid') })),
        password: z.string().min(1, { error: t('validation.passwordRequired') }),
      }),
    [t],
  )
  const form = useForm<z.input<typeof schema>, unknown, z.output<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  // Step 2 of SCR-AUT-01: GET /api/v1/admin/me decides where a signed-in user goes. Returns whether the page changes.
  async function continueSignedIn(): Promise<boolean> {
    let outcome: MeOutcome
    let failure: unknown
    try {
      outcome = meOutcome({ me: await queryClient.query(meQuery) })
    } catch (caught) {
      failure = caught
      outcome = meOutcome({ error: caught })
    }
    switch (outcome.kind) {
      case 'staff':
        router.replace(safeNext(params.get('next')) ?? outcome.landing)
        return true
      case 'change-password':
        router.replace(adminRoutes.changePassword)
        return true
      case 'sign-out':
        await signOutAndClear(queryClient)
        setError(t(`errors.${REASON_ERRORS[outcome.reason]}`))
        return false
      case 'account-refused':
      case 'sign-in-again':
      case 'error':
        // Signed in to Firebase but refused or unreachable at the API: never leave a half signed-in session. The
        // API's text when it answered (or the web's when it did not); a Firebase failure of the token says it in
        // the sign-in's own words.
        await signOutAndClear(queryClient)
        setError(
          failure instanceof ApiError || isNetworkError(failure)
            ? problemText(failure)
            : t('errors.generic'),
        )
        return false
    }
  }

  // A staff member who is already signed in goes on, once the SDK has read its session.
  useEffect(() => {
    let active = true
    hasSession()
      .then(async (signedIn) => {
        if (!active) return
        // The reason of a sign-out the admin layout made for a problem of the API: its text, shown once.
        const reason = takeSignOutReason()
        if (reason) setError(reason)
        setLeaving(signedIn)
        setSessionRead(true)
        if (signedIn) {
          const moved = await continueSignedIn()
          if (active) setLeaving(moved)
        }
      })
      // Firebase could not start or read its storage, or the sign-out failed: the form stays usable.
      .catch(() => {
        if (!active) return
        setLeaving(false)
        setSessionRead(true)
        setError(t('errors.generic'))
      })
    return () => {
      active = false
    }
    // The check runs once, when the page opens; continueSignedIn reads the params and router of that render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function onSubmit(values: z.output<typeof schema>) {
    setError(null)
    setPasswordChanged(false)
    try {
      await signInWithPassword(values.email, values.password)
    } catch (failure) {
      setError(tRoot(firebaseError(failure)))
      return
    }
    try {
      setLeaving(await continueSignedIn())
    } catch {
      // Only the sign-out after a refusal can fail here; the user can try again.
      setError(t('errors.generic'))
    }
  }

  const sending = form.formState.isSubmitting || leaving || !sessionRead

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle>
          <h1 className="text-xl">{t('title')}</h1>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            {passwordChanged && (
              <Alert role="status">
                <AlertDescription>{t('passwordChanged')}</AlertDescription>
              </Alert>
            )}
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="sign-in-email">{t('email')}</FieldLabel>
                  <Input
                    {...field}
                    id="sign-in-email"
                    type="email"
                    autoComplete="username"
                    spellCheck={false}
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Controller
              name="password"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="sign-in-password">{t('password')}</FieldLabel>
                  <InputGroup>
                    <InputGroupInput
                      {...field}
                      id="sign-in-password"
                      type={showPassword ? 'text' : 'password'}
                      autoComplete="current-password"
                      aria-invalid={fieldState.invalid}
                    />
                    <InputGroupAddon align="inline-end">
                      <InputGroupButton
                        size="icon-xs"
                        aria-label={t('showPassword')}
                        aria-pressed={showPassword}
                        onClick={() => setShowPassword((shown) => !shown)}
                      >
                        {showPassword ? <EyeOffIcon /> : <EyeIcon />}
                      </InputGroupButton>
                    </InputGroupAddon>
                  </InputGroup>
                  {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                </Field>
              )}
            />
            <Button type="submit" disabled={sending} aria-busy={sending}>
              {sending && (
                <LoaderCircleIcon data-icon="inline-start" className="motion-safe:animate-spin" />
              )}
              {t('submit')}
            </Button>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  )
}

/** The loading state of the sign-in page while its search parameters are read: the same layout as the form. */
export function SignInSkeleton() {
  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle>
          <Skeleton className="h-7 w-48" />
        </CardTitle>
      </CardHeader>
      <CardContent>
        <FieldGroup>
          {[0, 1].map((field) => (
            <Field key={field}>
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-9 w-full" />
            </Field>
          ))}
          <Skeleton className="h-9 w-full" />
        </FieldGroup>
      </CardContent>
    </Card>
  )
}
