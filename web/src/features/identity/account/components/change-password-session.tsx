'use client'

import { useTranslations } from 'next-intl'
import { createContext, useContext, type ReactNode } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { PASSWORD_CHANGED, useStaffSession, type StaffSession } from '@/features/identity/access'
import { useProblemText } from '@/hooks/use-problem-toast'
import { ChangePasswordForm } from './change-password-form'

// One session for the content and the actions: a second useStaffSession would redirect twice.
const SessionContext = createContext<StaffSession | null>(null)

function useSession(): StaffSession {
  const session = useContext(SessionContext)
  if (!session) throw new Error('The password change is used outside ChangePasswordSession')
  return session
}

/**
 * SCR-AUT-02 Required password change: shown alone, without the menu, while mustChangePassword is true (R-USR-03).
 * After the change the user is signed out and signs in again with the new password (DEC-094); "Đăng xuất" is the one
 * other action. A staff member without a pending change goes to their landing page.
 */
export function ChangePasswordSession({ children }: { children: ReactNode }) {
  const session = useStaffSession('change-password')
  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>
}

/** The content of SCR-AUT-02: the form once the session is known, or why it cannot be shown. */
export function ChangePasswordContent() {
  const session = useSession()
  const problemText = useProblemText()
  if (session.status === 'loading') return <Skeleton className="h-40 w-full" />
  if (session.status === 'error') {
    return (
      <Alert variant="destructive">
        <AlertDescription>{problemText(session.error)}</AlertDescription>
      </Alert>
    )
  }
  return (
    <ChangePasswordForm
      email={session.me.email}
      onChanged={() => session.signOut(PASSWORD_CHANGED)}
    />
  )
}

/** The actions of SCR-AUT-02: "Thử lại" after an error, and "Đăng xuất" once the session is known. */
export function ChangePasswordActions() {
  const t = useTranslations('admin')
  const session = useSession()
  if (session.status === 'loading') return null
  return (
    <>
      {session.status === 'error' && (
        <Button variant="outline" onClick={session.retry}>
          {t('retry')}
        </Button>
      )}
      <Button variant="outline" onClick={() => void session.signOut()}>
        {t('user.signOut')}
      </Button>
    </>
  )
}
