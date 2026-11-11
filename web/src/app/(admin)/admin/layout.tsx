import { headers } from 'next/headers'
import { connection } from 'next/server'
import { AppToaster } from '@/components/app/app-toaster'
import { QueryProvider } from '@/components/app/query-provider'
import { TooltipProvider } from '@/components/ui/tooltip'
import { ThemeProvider } from '@/features/shell/theme'
import { nonceOf } from '@/lib/security-headers'

/** The admin part: client pages behind sign-in that call the API (docs/FRONTEND.md, Rendering). */
export default async function AdminLayout({ children }: LayoutProps<'/admin'>) {
  // Every page of /admin renders at each request, so that Next.js puts the nonce of src/proxy.ts on its scripts
  // (DEC-113 (3)).
  await connection()
  // The inline script of the theme needs the same nonce, read from the CSP that src/proxy.ts set on the request.
  const nonce = nonceOf((await headers()).get('content-security-policy'))
  return (
    // Around everything, the sign-in page and the toasts included, so that all follow the chosen theme (DEC-123 (2)).
    <ThemeProvider nonce={nonce}>
      <QueryProvider>
        {/* The one provider of every tooltip of the part: the first opens after 700 ms, the next ones at once
            (docs/SCREENS.md, Help on screen). */}
        <TooltipProvider delayDuration={700} skipDelayDuration={300}>
          {children}
          <AppToaster />
        </TooltipProvider>
      </QueryProvider>
    </ThemeProvider>
  )
}
