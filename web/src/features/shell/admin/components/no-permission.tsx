'use client'

import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import type { ReactNode } from 'react'
import { adminRoutes, useCurrentUser } from '@/features/identity/access'
import { firstEntry } from '../utils/admin-menu'

/**
 * The no-permission page of SCR-ADM-01: "Bạn không có quyền xem trang này." and a link to the first menu entry the user
 * can open, other than this page, or to their account. Following the link also clears a 403 of this page (`onLeave`).
 */
export function NoPermission({ onLeave }: { onLeave?: () => void }) {
  const t = useTranslations('admin')
  const me = useCurrentUser()
  const pathname = usePathname()
  const entry = firstEntry(me.permissions, pathname)
  return (
    <div className="flex flex-col items-start gap-4">
      <h1 className="text-xl font-semibold">{t('noPermission')}</h1>
      <Link
        href={entry?.href ?? adminRoutes.account}
        onClick={onLeave}
        className="inline-flex min-h-6 items-center text-sm font-medium underline underline-offset-4"
      >
        {entry ? t(`menu.${entry.key}`) : t('user.account')}
      </Link>
    </div>
  )
}

/**
 * A page of the admin part with the permission of its endpoints: without it the user sees the no-permission page at
 * once instead of a 403 later. A convenience only: the API checks the permission of every call.
 */
export function RequirePermission({
  permission,
  children,
}: {
  permission: string
  children: ReactNode
}) {
  const me = useCurrentUser()
  return me.permissions.includes(permission) ? children : <NoPermission />
}
