import { AdminShell } from '@/features/identity/admin-shell'

/** SCR-ADM-01: every admin screen but the sign-in and the password change sits in this layout. */
export default function ShellLayout({ children }: LayoutProps<'/admin'>) {
  return <AdminShell>{children}</AdminShell>
}
