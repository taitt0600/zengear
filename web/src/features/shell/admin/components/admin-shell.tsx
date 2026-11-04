'use client'

import { useMemo, type ReactNode } from 'react'
import { CurrentUserProvider, useStaffSession } from '@/features/identity/access'
import { useProblemText } from '@/hooks/use-problem-toast'
import { AdminShellSkeleton } from './admin-shell-skeleton'
import { SessionError } from './session-error'

/**
 * SCR-ADM-01: the staff session of every admin screen but the sign-in and the password change. Its guard sends a
 * visitor away (useStaffSession); until GET /me answers it shows the skeleton, and the frame once the user is known.
 */
export function AdminShell({
  defaultOpen,
  children,
}: {
  defaultOpen: boolean
  children: ReactNode
}) {
  const session = useStaffSession('shell')
  const problemText = useProblemText()
  // One context value per user, sign-out and 403, so that the pages reading it re-render only when one changes.
  const ready = session.status === 'ready' ? session : undefined
  const me = ready?.me
  const signOut = ready?.signOut
  const forbidden = ready?.forbidden ?? false
  const clearForbidden = ready?.clearForbidden
  const current = useMemo(
    () => (me && signOut && clearForbidden ? { me, signOut, forbidden, clearForbidden } : null),
    [me, signOut, forbidden, clearForbidden],
  )

  if (session.status === 'error') {
    return (
      <SessionError
        text={problemText(session.error)}
        onRetry={session.retry}
        onSignOut={() => void session.signOut()}
      />
    )
  }
  if (!current) return <AdminShellSkeleton collapsed={!defaultOpen} />
  return <CurrentUserProvider value={current}>{children}</CurrentUserProvider>
}
