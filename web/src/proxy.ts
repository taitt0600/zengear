import { NextResponse, type NextRequest } from 'next/server'
import { env } from '@/env'
import { contentSecurityPolicy } from '@/lib/security-headers'

const development = process.env.NODE_ENV !== 'production'

// Fixed route redirects and the CSP with a nonce of /admin and /pos, never a check of the session (docs/FRONTEND.md;
// DEC-104 (10), DEC-113 (1), (5)): the admin layout sends a visitor without a session to the sign-in page, and the API
// answers 401 and 403. Until the storefront exists, the home page is the admin part.
export function proxy(request: NextRequest) {
  if (request.nextUrl.pathname === '/') {
    return NextResponse.redirect(new URL('/admin', request.url))
  }
  const nonce = Buffer.from(crypto.randomUUID()).toString('base64')
  const csp = contentSecurityPolicy(env, development, nonce)
  // Next.js reads the nonce from the CSP of the request when it renders the page.
  const headers = new Headers(request.headers)
  headers.set('Content-Security-Policy', csp)
  const response = NextResponse.next({ request: { headers } })
  response.headers.set('Content-Security-Policy', csp)
  return response
}

// Literal values only: Next.js reads the matcher when it builds. The router prefetches of /admin and /pos are skipped
// (fetched RSC payloads, never a document; the page they lead to gets its own nonce). A browser prefetch of the
// document (Purpose or Sec-Purpose: prefetch) is not, unlike the Next.js CSP guide: the browser may show it as the
// page, so it carries the CSP with a nonce (owner's decision on the security review of this change).
export const config = {
  matcher: [
    '/',
    {
      source: '/admin/:path*',
      missing: [{ type: 'header', key: 'next-router-prefetch' }],
    },
    {
      source: '/pos/:path*',
      missing: [{ type: 'header', key: 'next-router-prefetch' }],
    },
  ],
}
