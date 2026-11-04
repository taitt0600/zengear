import { cookies } from 'next/headers'
import {
  AdminHeaderSection,
  AdminLayout,
  AdminPageSection,
  AdminShell,
  AdminSidebarSection,
} from '@/features/shell/admin'

/** SCR-ADM-01: every admin screen but the sign-in and the password change sits in this layout. */
export default async function ShellLayout({ children }: LayoutProps<'/admin'>) {
  // The menu's state, which SidebarProvider keeps in this cookie (SIDEBAR_COOKIE_NAME of components/ui/sidebar.tsx,
  // written here again: a value exported by a 'use client' module is only a client reference on the server): open
  // when there is none.
  const defaultOpen = (await cookies()).get('sidebar_state')?.value !== 'false'
  return (
    <AdminShell defaultOpen={defaultOpen}>
      <AdminLayout
        defaultOpen={defaultOpen}
        sidebar={<AdminSidebarSection />}
        header={<AdminHeaderSection />}
      >
        <AdminPageSection>{children}</AdminPageSection>
      </AdminLayout>
    </AdminShell>
  )
}
