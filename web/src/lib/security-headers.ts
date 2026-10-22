import type { Env } from '@/env-schema'

// The security headers of every response (docs/FRONTEND.md, Security headers; DEC-104 (10)). The Firebase ID token
// lives in the browser storage of the SDK, so the CSP is the main guard against injected scripts: scripts and
// connections are limited to this site, the API and Firebase Authentication and Storage.
const FIREBASE_ORIGINS = [
  'https://identitytoolkit.googleapis.com',
  'https://securetoken.googleapis.com',
  'https://firebasestorage.googleapis.com',
]

// The static CSP of next.config.ts without a nonce, and the CSP of /admin and /pos with the nonce that src/proxy.ts
// makes for each request (DEC-113 (2)): one function, so that the two differ only in script-src.
export function contentSecurityPolicy(env: Env, development: boolean, nonce?: string): string {
  const connect = ["'self'", new URL(env.NEXT_PUBLIC_API_BASE_URL).origin, ...FIREBASE_ORIGINS]
  if (env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST) {
    connect.push(`http://${env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST}`)
  }
  // Next.js puts the nonce on its own scripts, and 'strict-dynamic' lets them load the others. Static pages carry no
  // nonce, so they need 'unsafe-inline' for the inline scripts of Next.js until the storefront Decision. React needs
  // 'unsafe-eval' in development only.
  const scripts = nonce
    ? ["'self'", `'nonce-${nonce}'`, "'strict-dynamic'"]
    : ["'self'", "'unsafe-inline'"]
  if (development) {
    scripts.push("'unsafe-eval'")
  }
  return [
    "default-src 'self'",
    `script-src ${scripts.join(' ')}`,
    // Radix sets style attributes, which a nonce does not cover.
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    `connect-src ${connect.join(' ')}`,
    `frame-src https://${env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join('; ')
}

// The headers of every path besides the CSP, which next.config.ts or src/proxy.ts sets (DEC-113 (4)).
export function securityHeaders(): { key: string; value: string }[] {
  return [
    // Without includeSubDomains until the production domain is chosen.
    { key: 'Strict-Transport-Security', value: 'max-age=63072000' },
    { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
    { key: 'X-Content-Type-Options', value: 'nosniff' },
  ]
}
