import { connection } from 'next/server'
import { AppToaster } from '@/components/app/app-toaster'
import { QueryProvider } from '@/components/app/query-provider'

/** The admin part: client pages behind sign-in that call the API (docs/FRONTEND.md, Rendering). */
export default async function AdminLayout({ children }: LayoutProps<'/admin'>) {
  // Every page of /admin renders at each request, so that Next.js puts the nonce of src/proxy.ts on its scripts
  // (DEC-113 (3)).
  await connection()
  return (
    <QueryProvider>
      {children}
      <AppToaster />
    </QueryProvider>
  )
}
