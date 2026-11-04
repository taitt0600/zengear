'use client'

import { ChevronRightIcon, Settings2Icon, UsersIcon, type LucideIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useState, type MouseEvent } from 'react'
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
import { visibleMenu, type MenuEntry, type MenuGroup } from '../utils/admin-menu'

const ICONS: Record<MenuEntry['key'] | MenuGroup['key'], LucideIcon | undefined> = {
  staff: UsersIcon,
  settings: Settings2Icon,
  businessLocation: undefined,
}

const isCurrent = (pathname: string, href: string) =>
  pathname === href || pathname.startsWith(`${href}/`)

/**
 * The menu of SCR-ADM-01, limited to the entries the user's permissions allow; without any, nothing (no group "Menu").
 * Collapsed, each entry names itself in a tooltip; on a narrow screen a link closes the sheet.
 */
export function AdminNav({ permissions }: { permissions: readonly string[] }) {
  const t = useTranslations('admin.menu')
  const pathname = usePathname()
  const { isMobile, setOpenMobile } = useSidebar()
  const items = visibleMenu(permissions)
  if (items.length === 0) return null
  const onNavigate = () => {
    if (isMobile) setOpenMobile(false)
  }

  return (
    <nav aria-label={t('title')}>
      <SidebarGroup>
        <SidebarGroupLabel>{t('title')}</SidebarGroupLabel>
        <SidebarMenu>
          {items.map((item) => {
            const Icon = ICONS[item.key]
            if ('entries' in item) {
              return (
                <NavGroup key={item.key} group={item} pathname={pathname} onNavigate={onNavigate} />
              )
            }
            const current = isCurrent(pathname, item.href)
            return (
              <SidebarMenuItem key={item.key}>
                <SidebarMenuButton asChild isActive={current} tooltip={t(item.key)}>
                  <Link
                    href={item.href}
                    aria-current={current ? 'page' : undefined}
                    onClick={onNavigate}
                  >
                    {Icon && <Icon aria-hidden />}
                    <span>{t(item.key)}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            )
          })}
        </SidebarMenu>
      </SidebarGroup>
    </nav>
  )
}

/** A group of the menu ("Cài đặt"): open while one of its entries is the current page; collapsed, a click opens both. */
function NavGroup({
  group,
  pathname,
  onNavigate,
}: {
  group: MenuGroup
  pathname: string
  onNavigate: () => void
}) {
  const t = useTranslations('admin.menu')
  const { state, setOpen: setSidebarOpen } = useSidebar()
  const holdsCurrent = group.entries.some((entry) => isCurrent(pathname, entry.href))
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
            {Icon && <Icon aria-hidden />}
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
              const current = isCurrent(pathname, entry.href)
              return (
                <SidebarMenuSubItem key={entry.key}>
                  <SidebarMenuSubButton asChild isActive={current}>
                    <Link
                      href={entry.href}
                      aria-current={current ? 'page' : undefined}
                      onClick={onNavigate}
                    >
                      <span>{t(entry.key)}</span>
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
