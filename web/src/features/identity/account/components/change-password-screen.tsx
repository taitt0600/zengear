'use client'

import { useTranslations } from 'next-intl'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { PASSWORD_CHANGED, useStaffSession } from '@/features/identity/access'
import { useProblemText } from '@/hooks/use-problem-toast'
import { ChangePasswordForm } from './change-password-form'

/**
 * SCR-AUT-02 Required password change: shown alone, without the menu, while mustChangePassword is true (R-USR-03).
 * After the change the user is signed out and signs in again with the new password (DEC-094); "Đăng xuất" is the one
 * other action. A staff member without a pending change goes to their landing page.
 */
export function ChangePasswordScreen() {
  const t = useTranslations()
  const session = useStaffSession('change-password')
  const problemText = useProblemText()

  return (
    <main className="flex flex-1 items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl">{t('changePassword.title')}</h1>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {session.status === 'loading' && <Skeleton className="h-40 w-full" />}
          {session.status === 'error' && (
            <Alert variant="destructive">
              <AlertDescription>{problemText(session.error)}</AlertDescription>
            </Alert>
          )}
          {session.status === 'ready' && (
            <ChangePasswordForm
              email={session.me.email}
              onChanged={() => session.signOut(PASSWORD_CHANGED)}
            />
          )}
        </CardContent>
        <CardFooter className="justify-end gap-2">
          {session.status === 'error' && (
            <Button variant="outline" onClick={session.retry}>
              {t('admin.retry')}
            </Button>
          )}
          {session.status !== 'loading' && (
            <Button variant="outline" onClick={() => void session.signOut()}>
              {t('admin.user.signOut')}
            </Button>
          )}
        </CardFooter>
      </Card>
    </main>
  )
}
