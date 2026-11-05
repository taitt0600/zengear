'use client'

import { ChevronsUpDownIcon, LogOutIcon, UserIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { useRef, useState } from 'react'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { adminRoutes } from '@/features/identity/access'
import { initials } from '@/utils/initials'

function UserName({ name, email }: { name: string; email: string | null }) {
  return (
    <>
      <Avatar className="size-8 rounded-lg" aria-hidden>
        <AvatarFallback className="rounded-lg">{initials(name)}</AvatarFallback>
      </Avatar>
      <div className="grid flex-1 text-left text-sm leading-tight">
        <span className="truncate font-medium">{name}</span>
        {email && <span className="truncate text-xs">{email}</span>}
      </div>
    </>
  )
}

const cutShort = (element: HTMLElement | null) =>
  [...(element?.querySelectorAll('.truncate') ?? [])].some(
    (text) => text.scrollWidth > text.clientWidth,
  )

/**
 * The signed-in user at the foot of the menu (SCR-ADM-01), with "Tài khoản của tôi" and "Đăng xuất". Its tooltip shows
 * the name when the menu is collapsed, and the name with the email when either is cut short.
 */
export function UserMenu({
  name,
  email,
  onSignOut,
}: {
  name: string
  email: string | null
  onSignOut: () => void
}) {
  const t = useTranslations('admin.user')
  const { isMobile, state, setOpenMobile } = useSidebar()
  const button = useRef<HTMLButtonElement>(null)
  const [tooltip, setTooltip] = useState(false)
  const collapsed = state === 'collapsed' && !isMobile

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <Tooltip
            open={tooltip}
            onOpenChange={(open) => setTooltip(open && (collapsed || cutShort(button.current)))}
          >
            <TooltipTrigger asChild>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton
                  ref={button}
                  size="lg"
                  aria-label={name}
                  className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
                >
                  <UserName name={name} email={email} />
                  <ChevronsUpDownIcon className="ml-auto size-4" aria-hidden />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
            </TooltipTrigger>
            <TooltipContent side="right" align="center">
              {collapsed || !email ? name : `${name} · ${email}`}
            </TooltipContent>
          </Tooltip>
          <DropdownMenuContent
            className="w-(--radix-dropdown-menu-trigger-width) min-w-56 rounded-lg"
            side={isMobile ? 'bottom' : 'right'}
            align="end"
            sideOffset={4}
          >
            <DropdownMenuLabel className="p-0 font-normal">
              <div className="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
                <UserName name={name} email={email} />
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              {/* On a narrow screen the menu is a sheet: the page it opens is not left behind it. */}
              <DropdownMenuItem asChild onSelect={() => isMobile && setOpenMobile(false)}>
                <Link href={adminRoutes.account}>
                  <UserIcon aria-hidden />
                  {t('account')}
                </Link>
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={onSignOut}>
                <LogOutIcon aria-hidden />
                {t('signOut')}
              </DropdownMenuItem>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
