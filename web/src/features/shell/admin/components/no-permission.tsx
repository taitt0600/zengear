'use client'

import { ShieldXIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { usePathname } from 'next/navigation'
import type { ReactNode } from 'react'
import { adminRoutes, useCurrentUser } from '@/features/identity/access'
import { firstEntry } from '../utils/admin-menu'
import { AdminPageStatus } from './admin-page-status'

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
    <AdminPageStatus
      icon={<ShieldXIcon aria-hidden />}
      title={t('noPermission')}
      description={t('noPermissionHint')}
      href={entry?.href ?? adminRoutes.account}
      label={entry ? t(`menu.${entry.key}`) : t('user.account')}
      onClick={onLeave}
    />
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
