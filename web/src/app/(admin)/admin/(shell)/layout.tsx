import { AdminShell } from '@/features/shell/admin'

/** SCR-ADM-01: every admin screen but the sign-in and the password change sits in this layout. */
export default function ShellLayout({ children }: LayoutProps<'/admin'>) {
  return <AdminShell>{children}</AdminShell>
}
