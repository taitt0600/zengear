import { useTranslations } from 'next-intl'
import type { ReactNode } from 'react'
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar'

/**
 * The layout of SCR-ADM-01 (block sidebar-07): the menu, the header and the page. The menu collapses to icons
 * (Ctrl/⌘+B too) and keeps its state in the cookie sidebar_state; below md it is a sheet from the left. The skip link
 * is the first focus stop, before the menu and the header.
 */
export function AdminLayout({
  defaultOpen,
  sidebar,
  header,
  children,
}: {
  defaultOpen: boolean
  sidebar: ReactNode
  header: ReactNode
  children: ReactNode
}) {
  const t = useTranslations('admin')
  return (
    <SidebarProvider defaultOpen={defaultOpen}>
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:bg-background focus:px-3 focus:py-2 focus:shadow"
      >
        {t('skipToContent')}
      </a>
      {sidebar}
      <SidebarInset>
        {header}
        <main
          id="main"
          tabIndex={-1}
          className="flex min-w-0 flex-1 flex-col p-4 focus:outline-none md:p-6"
        >
          {children}
        </main>
      </SidebarInset>
    </SidebarProvider>
  )
}
