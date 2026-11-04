'use client'

import { StoreIcon, XIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

/**
 * The store name at the top of the menu (sidebar-07's team-switcher), a link to /admin: "ZenGear" until gap 6 of
 * docs/SCREENS.md gives the store's own. On a narrow screen the sheet's close button sits beside it.
 */
export function AdminBrand() {
  const t = useTranslations()
  const { isMobile, setOpenMobile } = useSidebar()
  return (
    <SidebarMenu>
      <SidebarMenuItem className="flex items-center gap-1">
        <SidebarMenuButton asChild size="lg" tooltip={t('app.name')}>
          <Link href="/admin" onClick={() => isMobile && setOpenMobile(false)}>
            <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
              <StoreIcon className="size-4" aria-hidden />
            </div>
            <span className="truncate font-medium">{t('app.name')}</span>
          </Link>
        </SidebarMenuButton>
        {isMobile && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label={t('admin.menu.close')}
                onClick={() => setOpenMobile(false)}
              >
                <XIcon aria-hidden />
              </Button>
            </TooltipTrigger>
            <TooltipContent>{t('admin.menu.close')}</TooltipContent>
          </Tooltip>
        )}
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
