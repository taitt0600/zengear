import { describe, expect, it } from 'vitest'
import { contentSecurityPolicy, securityHeaders } from './security-headers'

const production = {
  NEXT_PUBLIC_API_BASE_URL: 'https://api.zengear.vn',
  NEXT_PUBLIC_FIREBASE_API_KEY: 'public-web-key',
  NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: 'zengear.firebaseapp.com',
  NEXT_PUBLIC_FIREBASE_PROJECT_ID: 'zengear',
}

const directives = (policy: string) =>
  Object.fromEntries(
    policy.split('; ').map((directive) => {
      const [name, ...values] = directive.split(' ')
      return [name, values]
    }),
  )

describe('contentSecurityPolicy', () => {
  it('lets the page connect only to itself, the API and Firebase', () => {
    expect(directives(contentSecurityPolicy(production, false))['connect-src']).toEqual([
      "'self'",
      'https://api.zengear.vn',
      'https://identitytoolkit.googleapis.com',
      'https://securetoken.googleapis.com',
      'https://firebasestorage.googleapis.com',
    ])
  })

  it('allows the emulator only when it is configured', () => {
    const csp = contentSecurityPolicy(
      { ...production, NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_HOST: '127.0.0.1:9099' },
      false,
    )
    expect(directives(csp)['connect-src']).toContain('http://127.0.0.1:9099')
  })

  it('refuses framing, plugins and other script sources', () => {
    const csp = directives(contentSecurityPolicy(production, false))
    expect(csp['frame-ancestors']).toEqual(["'none'"])
    expect(csp['object-src']).toEqual(["'none'"])
    expect(csp['script-src']).toEqual(["'self'", "'unsafe-inline'"])
    expect(csp['frame-src']).toEqual(['https://zengear.firebaseapp.com'])
  })

  it("adds 'unsafe-eval' in development only", () => {
    expect(directives(contentSecurityPolicy(production, true))['script-src']).toContain(
      "'unsafe-eval'",
    )
  })
})

describe('securityHeaders', () => {
  it('sets the headers of DEC-104 (10)', () => {
    const headers = Object.fromEntries(
      securityHeaders(production, false).map(({ key, value }) => [key, value]),
    )
    expect(Object.keys(headers)).toEqual([
      'Content-Security-Policy',
      'Strict-Transport-Security',
      'Referrer-Policy',
      'X-Content-Type-Options',
    ])
    expect(headers['Strict-Transport-Security']).toBe('max-age=63072000')
    expect(headers['Referrer-Policy']).toBe('strict-origin-when-cross-origin')
  })
})
