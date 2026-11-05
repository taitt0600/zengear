'use client'

import { useTranslations } from 'next-intl'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { Fragment } from 'react'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger, useSidebar } from '@/components/ui/sidebar'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { breadcrumbOf } from '../utils/admin-menu'

/**
 * The header of SCR-ADM-01: the menu button, named by what it does now, and where the page sits. A group of the menu
 * is text, an entry above the page a link, the page the current item; /admin and the 404 have none.
 */
export function AdminHeaderSection() {
  const t = useTranslations()
  const pathname = usePathname()
  const { isMobile, state } = useSidebar()
  const label = isMobile
    ? t('admin.menu.open')
    : state === 'expanded'
      ? t('admin.menu.collapse')
      : t('admin.menu.expand')
  const crumbs = breadcrumbOf(pathname)

  return (
    <header className="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear motion-reduce:transition-none group-has-data-[collapsible=icon]/sidebar-wrapper:h-12">
      <div className="flex items-center gap-2 px-4">
        <Tooltip>
          <TooltipTrigger asChild>
            <SidebarTrigger label={label} className="-ml-1" />
          </TooltipTrigger>
          <TooltipContent>{label}</TooltipContent>
        </Tooltip>
        <Separator orientation="vertical" className="mr-2 data-[orientation=vertical]:h-4" />
        {crumbs.length > 0 && (
          <Breadcrumb>
            <BreadcrumbList>
              {crumbs.map((crumb, index) => (
                <Fragment key={crumb.key}>
                  {index > 0 && <BreadcrumbSeparator />}
                  <BreadcrumbItem>
                    {index === crumbs.length - 1 ? (
                      <BreadcrumbPage>{t(crumb.key)}</BreadcrumbPage>
                    ) : crumb.href ? (
                      <BreadcrumbLink asChild>
                        <Link href={crumb.href}>{t(crumb.key)}</Link>
                      </BreadcrumbLink>
                    ) : (
                      t(crumb.key)
                    )}
                  </BreadcrumbItem>
                </Fragment>
              ))}
            </BreadcrumbList>
          </Breadcrumb>
        )}
      </div>
    </header>
  )
}
