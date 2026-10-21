import { NextResponse, type NextRequest } from 'next/server'

// Fixed route redirects only, never a check of the session (docs/FRONTEND.md; DEC-104 (10)): the admin layout sends a
// visitor without a session to the sign-in page, and the API answers 401 and 403. Until the storefront exists, the
// home page is the admin part.
export function proxy(request: NextRequest) {
  return NextResponse.redirect(new URL('/admin', request.url))
}

export const config = { matcher: '/' }
