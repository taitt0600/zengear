'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import type { z } from 'zod'
import {
  adminRoutes,
  firebaseError,
  meOutcome,
  meQuery,
  PASSWORD_CHANGED,
  safeNext,
  SIGN_OUT_REASONS,
  takeSignOutReason,
  type MeOutcome,
  type SignOutReason,
} from '@/features/identity/access'
import { useProblemText } from '@/hooks/use-problem-toast'
import { ApiError, isNetworkError } from '@/lib/api/problems'
import { hasSession, signInWithPassword, signOutAndClear } from '@/lib/auth/session'
import { signInSchema, type SignInValues } from '../schemas'

/** The reasons of a sign-out that the web words itself, as keys of `signIn.errors` (SCR-AUT-01). */
const REASON_ERRORS = {
  'account-disabled': 'accountDisabled',
  'not-staff': 'notStaff',
} as const satisfies Record<SignOutReason, string>

function reasonOf(reason: string | null): SignOutReason | undefined {
  return SIGN_OUT_REASONS.find((value) => value === reason)
}

/**
 * The behaviour of SCR-AUT-01 (UC-001, AUT-01): the session already open, `?next=` and `?reason=`, the sign-in with
 * Firebase, then GET /api/v1/admin/me, and the text of the alert.
 */
export function useSignIn() {
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
  // Until the SDK has read its session, and from the moment the user is signed in until the next page opens: the
  // button stays disabled, so a second click sends nothing.
  const [sessionRead, setSessionRead] = useState(false)
  const [leaving, setLeaving] = useState(false)

  const schema = useMemo(
    () =>
      signInSchema({
        emailRequired: t('validation.emailRequired'),
        emailInvalid: t('validation.emailInvalid'),
        passwordRequired: t('validation.passwordRequired'),
      }),
    [t],
  )
  const form = useForm<z.input<typeof schema>, unknown, SignInValues>({
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

  async function onSubmit(values: SignInValues) {
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
  return { form, onSubmit: form.handleSubmit(onSubmit), error, passwordChanged, sending }
}
