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
import { hasSession, signInWithPassword, signOutAndClear } from '@/lib/auth/session'
import { meQuery } from './me'
import {
  firebaseCode,
  meOutcome,
  SIGN_OUT_REASONS,
  type MeOutcome,
  type SignOutReason,
} from './me-outcome'
import { adminRoutes, safeNext } from './routes'

/** The answers of SCR-AUT-01 (docs/SCREENS.md), as keys of `signIn.errors`. */
export type SignInError =
  | 'wrongCredentials'
  | 'tooManyAttempts'
  | 'accountDisabled'
  | 'accountNotRegistered'
  | 'notStaff'
  | 'network'
  | 'generic'

const REASON_ERRORS: Record<SignOutReason, SignInError> = {
  'account-disabled': 'accountDisabled',
  'account-not-registered': 'accountNotRegistered',
  'not-staff': 'notStaff',
}

// Never which of the two was wrong (SCR-AUT-01).
const WRONG_CREDENTIALS = new Set([
  'auth/invalid-credential',
  'auth/invalid-login-credentials',
  'auth/wrong-password',
  'auth/user-not-found',
  'auth/invalid-email',
])

/** The answer of a failed Firebase sign-in, from its error code. */
export function firebaseError(error: unknown): SignInError {
  const code = firebaseCode(error) ?? ''
  if (WRONG_CREDENTIALS.has(code)) return 'wrongCredentials'
  if (code === 'auth/too-many-requests') return 'tooManyAttempts'
  // ZenGear never disables the Firebase account itself (R-SEC-04 revokes its sessions), but the console can.
  if (code === 'auth/user-disabled') return 'accountDisabled'
  if (code === 'auth/network-request-failed') return 'network'
  return 'generic'
}

function reasonError(reason: string | null): SignInError | null {
  const known = SIGN_OUT_REASONS.find((value) => value === reason)
  return known ? REASON_ERRORS[known] : null
}

/** SCR-AUT-01: staff sign in with their company email and password (UC-001, AUT-01). */
export function SignInForm() {
  const t = useTranslations('signIn')
  const router = useRouter()
  const params = useSearchParams()
  const queryClient = useQueryClient()
  const [error, setError] = useState<SignInError | null>(() => reasonError(params.get('reason')))
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
    try {
      outcome = meOutcome({ me: await queryClient.query(meQuery) })
    } catch (failure) {
      outcome = meOutcome({ error: failure })
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
        setError(REASON_ERRORS[outcome.reason])
        return false
      case 'sign-in-again':
      case 'error':
        // Signed in to Firebase but refused or unreachable at the API: never leave a half signed-in session.
        await signOutAndClear(queryClient)
        setError(outcome.kind === 'error' && outcome.network ? 'network' : 'generic')
        return false
    }
  }

  // A staff member who is already signed in goes on, once the SDK has read its session.
  useEffect(() => {
    let active = true
    hasSession()
      .then(async (signedIn) => {
        if (!active) return
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
        setError('generic')
      })
    return () => {
      active = false
    }
    // The check runs once, when the page opens; continueSignedIn reads the params and router of that render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function onSubmit(values: z.output<typeof schema>) {
    setError(null)
    try {
      await signInWithPassword(values.email, values.password)
    } catch (failure) {
      setError(firebaseError(failure))
      return
    }
    try {
      setLeaving(await continueSignedIn())
    } catch {
      // Only the sign-out after a refusal can fail here; the user can try again.
      setError('generic')
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
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{t(`errors.${error}`)}</AlertDescription>
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
