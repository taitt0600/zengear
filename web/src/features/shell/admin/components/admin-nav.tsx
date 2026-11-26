'use client'

import { ChevronRightIcon, Settings2Icon, UsersIcon, type LucideIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { usePathname, useSearchParams } from 'next/navigation'
import { useState, type MouseEvent } from 'react'
import { Badge } from '@/components/ui/badge'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import {
  SidebarGroup,
  SidebarGroupLabel,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { usePendingStaffCount } from '@/hooks/use-pending-staff-count'
import { currentEntry, visibleMenu, type MenuEntry, type MenuGroup } from '../utils/admin-menu'

const ICONS: Record<MenuGroup['key'], LucideIcon> = { staff: UsersIcon, settings: Settings2Icon }

/**
 * The menu of SCR-ADM-01, limited to the entries the user's permissions allow; without any, nothing (no group "Menu").
 * Each area is a group with a submenu, open while one of its entries is the current page; "Chờ duyệt" (STF-09) shows
 * the number of accounts waiting, hidden at 0, while it loads and when it fails. Collapsed, each group names itself in
 * a tooltip; on a narrow screen a link closes the sheet.
 */
export function AdminNav({ permissions }: { permissions: readonly string[] }) {
  const t = useTranslations('admin.menu')
  const pathname = usePathname()
  const status = useSearchParams().get('status')
  const { isMobile, setOpenMobile } = useSidebar()
  const groups = visibleMenu(permissions)
  const current = currentEntry(pathname, status, permissions)
  const pending = usePendingStaffCount(
    groups.some((group) => group.entries.some((entry) => entry.key === 'staffPending')),
  )
  if (groups.length === 0) return null
  const onNavigate = () => {
    if (isMobile) setOpenMobile(false)
  }

  return (
    <nav aria-label={t('title')}>
      <SidebarGroup>
        <SidebarGroupLabel>{t('title')}</SidebarGroupLabel>
        <SidebarMenu>
          {groups.map((group) => (
            <NavGroup
              key={group.key}
              group={group}
              current={current}
              pathname={pathname}
              pending={pending}
              onNavigate={onNavigate}
            />
          ))}
        </SidebarMenu>
      </SidebarGroup>
    </nav>
  )
}

/** A group of the menu: open while one of its entries is the current page; collapsed, a click opens both. */
function NavGroup({
  group,
  current,
  pathname,
  pending,
  onNavigate,
}: {
  group: MenuGroup
  current: MenuEntry | undefined
  pathname: string
  pending: number | undefined
  onNavigate: () => void
}) {
  const t = useTranslations('admin.menu')
  const { state, setOpen: setSidebarOpen } = useSidebar()
  const holdsCurrent = !!current && group.entries.includes(current)
  const [open, setOpen] = useState(holdsCurrent)
  // A new path that one of its entries opens opens the group (the layout stays mounted across pages).
  const [shownPath, setShownPath] = useState(pathname)
  if (shownPath !== pathname) {
    setShownPath(pathname)
    if (holdsCurrent) setOpen(true)
  }
  const Icon = ICONS[group.key]

  const expandMenu = (event: MouseEvent) => {
    if (state !== 'collapsed') return
    // The trigger's own toggle would close a group already open: the menu opens with the group open instead.
    event.preventDefault()
    setSidebarOpen(true)
    setOpen(true)
  }

  return (
    <Collapsible asChild open={open} onOpenChange={setOpen} className="group/collapsible">
      <SidebarMenuItem>
        <CollapsibleTrigger asChild>
          <SidebarMenuButton tooltip={t(group.key)} onClick={expandMenu}>
            <Icon aria-hidden />
            <span>{t(group.key)}</span>
            <ChevronRightIcon
              aria-hidden
              className="ml-auto transition-transform duration-200 motion-reduce:transition-none group-data-[state=open]/collapsible:rotate-90"
            />
          </SidebarMenuButton>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <SidebarMenuSub>
            {group.entries.map((entry) => {
              const isCurrent = entry === current
              const count = entry.key === 'staffPending' && pending ? pending : undefined
              return (
                <SidebarMenuSubItem key={entry.key}>
                  <SidebarMenuSubButton asChild isActive={isCurrent}>
                    <Link
                      href={entry.href}
                      aria-current={isCurrent ? 'page' : undefined}
                      onClick={onNavigate}
                    >
                      <span>{t(entry.key)}</span>
                      {count !== undefined && (
                        <Badge className="ml-auto">
                          <span aria-hidden>{count}</span>
                          <span className="sr-only">{t('pendingCount', { count })}</span>
                        </Badge>
                      )}
                    </Link>
                  </SidebarMenuSubButton>
                </SidebarMenuSubItem>
              )
            })}
          </SidebarMenuSub>
        </CollapsibleContent>
      </SidebarMenuItem>
    </Collapsible>
  )
}
