// @vitest-environment node
import { unstable_doesMiddlewareMatch } from 'next/experimental/testing/server'
import { NextRequest } from 'next/server'
import { describe, expect, it } from 'vitest'
import { config, proxy } from './proxy'

const request = (path: string) => new NextRequest(new URL(path, 'http://localhost:3000'))
const nonceOf = (csp: string | null) => /'nonce-([^']+)'/.exec(csp ?? '')?.[1]

describe('proxy', () => {
  it('sets the same CSP with a nonce on the request and on the response (DEC-113 (1))', () => {
    const response = proxy(request('/admin/staff'))
    const csp = response.headers.get('Content-Security-Policy')

    expect(nonceOf(csp)).toMatch(/^[A-Za-z0-9+/]+={0,2}$/)
    expect(csp).toContain("'strict-dynamic'")
    // NextResponse.next({ request: { headers } }) hands the request headers to the render this way.
    expect(response.headers.get('x-middleware-request-content-security-policy')).toBe(csp)
  })

  it('makes a new nonce for every request', () => {
    const first = proxy(request('/admin')).headers.get('Content-Security-Policy')
    const second = proxy(request('/admin')).headers.get('Content-Security-Policy')
    expect(nonceOf(first)).not.toBe(nonceOf(second))
  })

  it('still sends the home page to the admin part', () => {
    const response = proxy(request('/'))
    expect(response.headers.get('Location')).toBe('http://localhost:3000/admin')
    expect(response.headers.get('Content-Security-Policy')).toBeNull()
  })
})

describe('config.matcher', () => {
  const matches = (url: string, headers?: Record<string, string>) =>
    unstable_doesMiddlewareMatch({ config, url, headers })

  it.each(['/', '/admin', '/admin/staff', '/admin/settings/business-location', '/pos', '/pos/x'])(
    'runs on %s',
    (url) => {
      expect(matches(url)).toBe(true)
    },
  )

  it.each(['/administrator', '/posts', '/other'])('leaves %s to the static CSP', (url) => {
    expect(matches(url)).toBe(false)
  })

  it('skips the prefetches of /admin and /pos', () => {
    expect(matches('/admin/staff', { 'next-router-prefetch': '1' })).toBe(false)
    expect(matches('/pos', { purpose: 'prefetch' })).toBe(false)
  })

  it('still redirects a prefetch of the home page', () => {
    expect(matches('/', { 'next-router-prefetch': '1' })).toBe(true)
  })
})
