import { describe, expect, it } from 'vitest'
import { contentSecurityPolicy, securityHeaders } from './security-headers'

const production = {
  NEXT_PUBLIC_API_BASE_URL: 'https://api.zengear.vn',
  NEXT_PUBLIC_FIREBASE_API_KEY: 'public-web-key',
  NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN: 'zengear.firebaseapp.com',
  NEXT_PUBLIC_FIREBASE_PROJECT_ID: 'zengear',
}

const NONCE = 'MDAwMDAwMDAtMDAwMC00MDAwLTgwMDAtMDAwMDAwMDAwMDAw'

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

  it('keeps the static CSP of the pages without a nonce', () => {
    expect(contentSecurityPolicy(production, false)).toBe(
      "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; font-src 'self'; connect-src 'self' https://api.zengear.vn " +
        'https://identitytoolkit.googleapis.com https://securetoken.googleapis.com ' +
        'https://firebasestorage.googleapis.com; frame-src https://zengear.firebaseapp.com; ' +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'",
    )
  })

  it("allows the scripts of the nonce only, without 'unsafe-inline' (DEC-113 (2))", () => {
    expect(directives(contentSecurityPolicy(production, false, NONCE))['script-src']).toEqual([
      "'self'",
      `'nonce-${NONCE}'`,
      "'strict-dynamic'",
    ])
  })

  it('changes only script-src when a nonce is given', () => {
    const { 'script-src': withNonce, ...others } = directives(
      contentSecurityPolicy(production, false, NONCE),
    )
    const { 'script-src': without, ...staticOthers } = directives(
      contentSecurityPolicy(production, false),
    )
    expect(withNonce).not.toEqual(without)
    expect(others).toEqual(staticOthers)
    expect(others['style-src']).toEqual(["'self'", "'unsafe-inline'"])
  })

  it.each([undefined, NONCE])("adds 'unsafe-eval' in development only (nonce %s)", (nonce) => {
    const script = (development: boolean) =>
      directives(contentSecurityPolicy(production, development, nonce))['script-src']
    expect(script(true)).toContain("'unsafe-eval'")
    expect(script(false)).not.toContain("'unsafe-eval'")
  })
})

describe('securityHeaders', () => {
  it('sets the other headers of DEC-104 (10) on every path', () => {
    const headers = Object.fromEntries(securityHeaders().map(({ key, value }) => [key, value]))
    expect(Object.keys(headers)).toEqual([
      'Strict-Transport-Security',
      'Referrer-Policy',
      'X-Content-Type-Options',
    ])
    expect(headers['Strict-Transport-Security']).toBe('max-age=63072000')
    expect(headers['Referrer-Policy']).toBe('strict-origin-when-cross-origin')
  })
})
