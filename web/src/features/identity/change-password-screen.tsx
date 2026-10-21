'use client'

import { useTranslations } from 'next-intl'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useStaffSession } from './staff-session'

/**
 * SCR-AUT-02 until STF-03 builds it: shown alone while mustChangePassword is true, it says the screen comes later and
 * offers "Đăng xuất", the one other action of SCR-AUT-02. A staff member without a pending change goes to their
 * landing page.
 */
export function ChangePasswordScreen() {
  const t = useTranslations()
  const session = useStaffSession('change-password')

  return (
    <main className="flex flex-1 items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl">{t('changePassword.title')}</h1>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {session.status === 'loading' && <Skeleton className="h-10 w-full" />}
          {session.status === 'error' && (
            <Alert variant="destructive">
              <AlertDescription>{t('problems.unknown')}</AlertDescription>
            </Alert>
          )}
          {session.status === 'ready' && (
            <p className="text-sm text-muted-foreground">{t('changePassword.placeholder')}</p>
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
