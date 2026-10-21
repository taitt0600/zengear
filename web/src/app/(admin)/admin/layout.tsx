import { QueryProvider } from '@/components/app/query-provider'

/** The admin part: client pages behind sign-in that call the API (docs/FRONTEND.md, Rendering). */
export default function AdminLayout({ children }: LayoutProps<'/admin'>) {
  return <QueryProvider>{children}</QueryProvider>
}
