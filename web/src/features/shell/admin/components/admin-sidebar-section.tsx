'use client'

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarRail,
} from '@/components/ui/sidebar'
import { useCurrentUser, useStaffSignOut } from '@/features/identity/access'
import { AdminBrand } from './admin-brand'
import { AdminNav } from './admin-nav'
import { UserMenu } from './user-menu'

/** The menu of SCR-ADM-01 (sidebar-07's app-sidebar): the store name, the entries the user may open, the user. */
export function AdminSidebarSection() {
  const me = useCurrentUser()
  const signOut = useStaffSignOut()
  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <AdminBrand />
      </SidebarHeader>
      <SidebarContent>
        <AdminNav permissions={me.permissions} />
      </SidebarContent>
      <SidebarFooter>
        <UserMenu name={me.name} email={me.email} onSignOut={() => void signOut()} />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
