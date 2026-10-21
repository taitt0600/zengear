'use client'

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { usePathname, useRouter } from 'next/navigation'
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { onApiError } from '@/lib/api/api-errors'
import { ApiError, problemName } from '@/lib/api/problems'
import { signOutAndClear, useAuthState } from '@/lib/auth/session'
import { meQuery, type CurrentUser } from './me'
import { meOutcome, sessionOutcome, type MeOutcome } from './me-outcome'
import { adminRoutes, signInPath } from './routes'

export type StaffSession =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void; signOut: () => Promise<void> }
  | {
      status: 'ready'
      me: CurrentUser
      forbidden: boolean
      clearForbidden: () => void
      signOut: () => Promise<void>
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
  const auth = useAuthState()
  const me = useQuery({ ...meQuery, enabled: auth === 'signed-in' })
  // Once the page is left (a redirect or a sign-out), nothing else is decided here.
  const leaving = useRef(false)
  const [failed, setFailed] = useState(false)
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
    (path: string, signOut: boolean) => leave(path, signOut).catch(() => setFailed(true)),
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
        case 'sign-in-again':
          void leaveOrFail(signInPath({ next: here }), true)
          return
        case 'error':
          return
      }
    },
    [leaveOrFail, mode],
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
          problemName(error.problem) === 'permission-denied'
        ) {
          setForbiddenPath(window.location.pathname)
        }
      }),
    [follow],
  )

  const signOut = useCallback(() => leaveOrFail(adminRoutes.signIn, true), [leaveOrFail])

  if (failed || outcome?.kind === 'error') {
    const retry = () => {
      setFailed(false)
      void me.refetch()
    }
    return { status: 'error', retry, signOut }
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

const CurrentUserContext = createContext<CurrentUser | null>(null)

export const CurrentUserProvider = CurrentUserContext.Provider

/** The signed-in staff member, inside the admin layout. */
export function useCurrentUser(): CurrentUser {
  const me = useContext(CurrentUserContext)
  if (!me) throw new Error('useCurrentUser is used outside the admin layout')
  return me
}
