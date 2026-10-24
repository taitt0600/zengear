'use client'

import { MenuIcon, XIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { useMemo, useState, type ReactNode } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { AdminNav } from './admin-nav'
import { NoPermission } from './no-permission'
import { CurrentUserProvider, useStaffSession } from './staff-session'
import { UserMenu } from './user-menu'

/** SCR-ADM-01: the layout of every admin screen but the sign-in and the password change, behind the guard. */
export function AdminShell({ children }: { children: ReactNode }) {
  const t = useTranslations()
  const session = useStaffSession('shell')
  const [menuOpen, setMenuOpen] = useState(false)
  // One context value per user and sign-out, so that the pages reading it re-render only when one of them changes.
  const readyMe = session.status === 'ready' ? session.me : null
  const signOut = session.status === 'loading' ? null : session.signOut
  const current = useMemo(
    () => (readyMe && signOut ? { me: readyMe, signOut } : null),
    [readyMe, signOut],
  )

  if (session.status === 'loading') return <AdminShellSkeleton />
  if (session.status === 'error') {
    return (
      <main className="flex flex-1 items-center justify-center p-4">
        <div className="flex w-full max-w-sm flex-col gap-4">
          <Alert variant="destructive">
            <AlertDescription>{t('problems.unknown')}</AlertDescription>
          </Alert>
          <div className="flex gap-2">
            <Button onClick={session.retry}>{t('admin.retry')}</Button>
            <Button variant="outline" onClick={() => void session.signOut()}>
              {t('admin.user.signOut')}
            </Button>
          </div>
        </div>
      </main>
    )
  }

  // Never null here, since the session is ready: the check only narrows the type.
  if (!current) return <AdminShellSkeleton />
  const { me } = current
  return (
    <CurrentUserProvider value={current}>
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:bg-background focus:px-3 focus:py-2 focus:shadow"
      >
        {t('admin.skipToContent')}
      </a>
      <header className="flex h-14 items-center gap-2 border-b px-4">
        <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
          <SheetTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="md:hidden"
              aria-label={t('admin.menu.open')}
            >
              <MenuIcon aria-hidden />
            </Button>
          </SheetTrigger>
          {/* The header below has its own close button, so the sheet's corner one is off. */}
          <SheetContent side="left" showCloseButton={false} className="w-64">
            <SheetHeader className="flex-row items-center justify-between">
              <SheetTitle>{t('admin.menu.title')}</SheetTitle>
              <SheetClose asChild>
                <Button variant="ghost" size="icon" aria-label={t('admin.menu.close')}>
                  <XIcon aria-hidden />
                </Button>
              </SheetClose>
            </SheetHeader>
            <AdminNav permissions={me.permissions} onNavigate={() => setMenuOpen(false)} />
          </SheetContent>
        </Sheet>
        <Link href="/admin" className="font-semibold">
          {t('app.name')}
        </Link>
        <div className="ml-auto">
          <UserMenu name={me.name} onSignOut={() => void session.signOut()} />
        </div>
      </header>
      <div className="flex flex-1">
        <aside className="hidden w-60 shrink-0 border-r md:block">
          <AdminNav permissions={me.permissions} />
        </aside>
        <main id="main" tabIndex={-1} className="min-w-0 flex-1 p-6 focus:outline-none">
          {session.forbidden ? <NoPermission onLeave={session.clearForbidden} /> : children}
        </main>
      </div>
    </CurrentUserProvider>
  )
}

/** The layout while the session and GET /me load: header, menu and page in place, never an empty screen. */
export function AdminShellSkeleton() {
  return (
    <>
      <div className="flex h-14 items-center gap-4 border-b px-4">
        <Skeleton className="h-5 w-24" />
        <Skeleton className="ml-auto h-8 w-32" />
      </div>
      <div className="flex flex-1">
        <div className="hidden w-60 shrink-0 flex-col gap-2 border-r p-3 md:flex">
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-full" />
        </div>
        <div className="flex flex-1 flex-col gap-4 p-6">
          <Skeleton className="h-7 w-64" />
          <Skeleton className="h-5 w-96 max-w-full" />
        </div>
      </div>
    </>
  )
}
