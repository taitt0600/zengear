import type { Env } from '@/env-schema'

// The security headers of every response (docs/FRONTEND.md, Security headers; DEC-104 (10)). The Firebase ID token
// lives in the browser storage of the SDK, so the CSP is the main guard against injected scripts: scripts and
// connections are limited to this site, the API and Firebase Authentication and Storage.
const FIREBASE_ORIGINS = [
  'https://identitytoolkit.googleapis.com',
  'https://securetoken.googleapis.com',
  'https://firebasestorage.googleapis.com',
]

export function contentSecurityPolicy(env: Env, development: boolean): string {
  const connect = ["'self'", new URL(env.NEXT_PUBLIC_API_BASE_URL).origin, ...FIREBASE_ORIGINS]
  if (env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST) {
    connect.push(`http://${env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST}`)
  }
  return [
    "default-src 'self'",
    // Static pages carry no nonce, so Next.js needs 'unsafe-inline' for its inline scripts (owner's answer 3; a
    // Decision on a nonce for /admin and /pos comes before STF-03); React needs 'unsafe-eval' in development only.
    `script-src 'self' 'unsafe-inline'${development ? " 'unsafe-eval'" : ''}`,
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

export function securityHeaders(env: Env, development: boolean): { key: string; value: string }[] {
  return [
    { key: 'Content-Security-Policy', value: contentSecurityPolicy(env, development) },
    // Without includeSubDomains until the production domain is chosen.
    { key: 'Strict-Transport-Security', value: 'max-age=63072000' },
    { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
    { key: 'X-Content-Type-Options', value: 'nosniff' },
  ]
}
