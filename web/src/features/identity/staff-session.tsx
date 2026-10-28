'use client'

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { usePathname, useRouter } from 'next/navigation'
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { onApiError } from '@/lib/api/api-errors'
import { ApiError, hasProblemType } from '@/lib/api/problems'
import { signOutAndClear, useAuthState } from '@/lib/auth/session'
import { meQuery, type CurrentUser } from './me'
import { meOutcome, sessionOutcome, type MeOutcome } from './me-outcome'
import { useProblemText } from './problem-toast'
import { adminRoutes, signInPath, type PASSWORD_CHANGED } from './routes'
import { keepSignOutReason } from './sign-out-reason'

/** Signs out to the sign-in page; after a password change, with its notice there. */
export type SignOut = (reason?: typeof PASSWORD_CHANGED) => Promise<void>

export type StaffSession =
  | { status: 'loading' }
  // `error`: what failed, GET /me or the sign-out, for the text of the error state.
  | { status: 'error'; error: unknown; retry: () => void; signOut: SignOut }
  | {
      status: 'ready'
      me: CurrentUser
      forbidden: boolean
      clearForbidden: () => void
      signOut: SignOut
    }

/**
 * The guard of the admin part (docs/SCREENS.md, SCR-ADM-01 and Conventions for every screen): without a Firebase
 * session every admin route goes to the sign-in page with the path kept as ?next=; GET /api/v1/admin/me then decides,
 * through the one mapping of me-outcome.ts. `mode` is the page: the layout lets staff in and sends a pending password
 * change to SCR-AUT-02; the password change page does the reverse. A failed call anywhere follows the same rules, and
 * 403 permission-denied shows the no-permission page in place of the page until the path changes.
 * Hiding a page is not authorization: the API still answers 401 and 403.
 */
export function useStaffSession(mode: 'shell' | 'change-password'): StaffSession {
  const router = useRouter()
  const pathname = usePathname()
  const queryClient = useQueryClient()
  const problemText = useProblemText()
  const auth = useAuthState()
  const me = useQuery({ ...meQuery, enabled: auth === 'signed-in' })
  // Once the page is left (a redirect or a sign-out), nothing else is decided here.
  const leaving = useRef(false)
  // The sign-out failed: its error, kept until "Thử lại".
  const [failed, setFailed] = useState<{ error: unknown } | null>(null)
  const [forbiddenPath, setForbiddenPath] = useState<string | null>(null)
  // The layout stays mounted across pages: a new path clears the no-permission page of the last one.
  const [shownPath, setShownPath] = useState(pathname)
  if (shownPath !== pathname) {
    setShownPath(pathname)
    setForbiddenPath(null)
  }

  const outcome = useMemo((): MeOutcome | undefined => {
    if (auth !== 'signed-in') return undefined
    // A failed refetch (on focus, for one) keeps the old data: a session answer still decides (401, a disabled
    // account); another failure keeps the page.
    const session = me.error ? sessionOutcome(me.error) : undefined
    if (session) return session
    if (me.data) return meOutcome({ me: me.data })
    if (me.error) return meOutcome({ error: me.error })
    return undefined
  }, [auth, me.data, me.error])

  const leave = useCallback(
    async (path: string, signOut: boolean) => {
      if (leaving.current) return
      leaving.current = true
      try {
        if (signOut) await signOutAndClear(queryClient)
      } catch (error) {
        leaving.current = false
        throw error
      }
      router.replace(path)
    },
    [queryClient, router],
  )
  // Firebase could not sign out: the error state offers to try again or to sign out again.
  const leaveOrFail = useCallback(
    (path: string, signOut: boolean) =>
      leave(path, signOut).catch((error: unknown) => setFailed({ error })),
    [leave],
  )

  const follow = useCallback(
    (next: MeOutcome) => {
      const here = window.location.pathname + window.location.search
      switch (next.kind) {
        case 'staff':
          if (mode === 'change-password') void leaveOrFail(next.landing, false)
          return
        case 'change-password':
          if (mode === 'shell') void leaveOrFail(adminRoutes.changePassword, false)
          return
        case 'sign-out':
          void leaveOrFail(signInPath({ reason: next.reason }), true)
          return
        case 'account-refused':
          // The sign-in page shows the API's text once (DEC-117 (4)).
          if (!leaving.current) keepSignOutReason(problemText(next.error))
          void leaveOrFail(adminRoutes.signIn, true)
          return
        case 'sign-in-again':
          void leaveOrFail(signInPath({ next: here }), true)
          return
        case 'error':
          return
      }
    },
    [leaveOrFail, mode, problemText],
  )

  useEffect(() => {
    if (auth === 'signed-out') {
      void leaveOrFail(
        signInPath({ next: window.location.pathname + window.location.search }),
        false,
      )
    } else if (outcome) {
      follow(outcome)
    }
  }, [auth, outcome, follow, leaveOrFail])

  // The other calls of the page: the same session rules, and the no-permission page for 403 permission-denied.
  useEffect(
    () =>
      onApiError((error) => {
        const session = sessionOutcome(error)
        if (session) follow(session)
        else if (
          error instanceof ApiError &&
          error.status === 403 &&
          hasProblemType(error.problem, 'permission-denied')
        ) {
          setForbiddenPath(window.location.pathname)
        }
      }),
    [follow],
  )

  // Through leave(), which marks the page as left before Firebase signs out: the signed-out effect above then
  // adds no ?next= redirect of its own.
  const signOut = useCallback<SignOut>(
    (reason) => leaveOrFail(reason ? signInPath({ reason }) : adminRoutes.signIn, true),
    [leaveOrFail],
  )

  if (failed || outcome?.kind === 'error') {
    const retry = () => {
      setFailed(null)
      void me.refetch()
    }
    return { status: 'error', error: failed ? failed.error : me.error, retry, signOut }
  }
  const allowed = mode === 'shell' ? 'staff' : 'change-password'
  // After a sign-out the cache is empty and the session signed out, so this is loading again.
  if (me.data && outcome?.kind === allowed) {
    return {
      status: 'ready',
      me: me.data,
      forbidden: forbiddenPath === pathname,
      clearForbidden: () => setForbiddenPath(null),
      signOut,
    }
  }
  return { status: 'loading' }
}

const CurrentUserContext = createContext<{ me: CurrentUser; signOut: SignOut } | null>(null)

export const CurrentUserProvider = CurrentUserContext.Provider

function useStaffContext() {
  const context = useContext(CurrentUserContext)
  if (!context) throw new Error('The staff session is used outside the admin layout')
  return context
}

/** The signed-in staff member, inside the admin layout. */
export function useCurrentUser(): CurrentUser {
  return useStaffContext().me
}

/** The sign-out of the admin layout, for a page inside it. */
export function useStaffSignOut(): SignOut {
  return useStaffContext().signOut
}
