import { expect, test } from '@playwright/test'

// The CSP with a nonce of /admin and /pos and the static CSP of the other paths (DEC-113 (1), (4), (6)), against
// next dev locally and next start in CI.

type HeaderList = { name: string; value: string }[]

const cspHeaders = (headers: HeaderList) =>
  headers
    .filter(({ name }) => name.toLowerCase() === 'content-security-policy')
    .map(({ value }) => value)

const nonceOf = (csp: string) => /'nonce-([^']+)'/.exec(csp)?.[1]
const scriptSrc = (csp: string) => /script-src ([^;]*)/.exec(csp)?.[1] ?? ''

test('every load of an admin page carries one CSP with a new nonce, which its scripts carry', async ({
  page,
}) => {
  const nonces: string[] = []
  for (let load = 0; load < 2; load++) {
    const response = await page.goto('/admin/sign-in')
    const csp = cspHeaders(await response!.headersArray())
    expect(csp).toHaveLength(1)
    expect(scriptSrc(csp[0])).toContain("'strict-dynamic'")
    expect(scriptSrc(csp[0])).not.toContain("'unsafe-inline'")
    const nonce = nonceOf(csp[0])!
    expect(nonce).toBeTruthy()
    // Every script of the server's HTML carries the nonce; those its scripts add later need none ('strict-dynamic').
    const scripts = (await response!.text()).match(/<script\b[^>]*>/g) ?? []
    expect(scripts.length).toBeGreaterThan(0)
    for (const script of scripts) {
      expect(script).toContain(`nonce="${nonce}"`)
    }
    nonces.push(nonce)
  }
  expect(nonces[0]).not.toBe(nonces[1])
})

test('only /admin and /pos leave the static CSP, router prefetches aside', async ({ request }) => {
  for (const path of ['/admin', '/admin/staff', '/pos']) {
    const csp = cspHeaders((await request.get(path, { maxRedirects: 0 })).headersArray())
    expect(csp, path).toHaveLength(1)
    expect(nonceOf(csp[0]), path).toBeTruthy()
  }
  // A browser prefetch of the document may be shown as the page: it gets the CSP with a nonce. A router prefetch is
  // an RSC payload, never a document, and skips the proxy.
  const prefetch = (headers: Record<string, string>) =>
    request
      .get('/admin/staff', { headers, maxRedirects: 0 })
      .then((r) => cspHeaders(r.headersArray()))
  const browserPrefetch = await prefetch({ purpose: 'prefetch', 'sec-purpose': 'prefetch' })
  expect(browserPrefetch).toHaveLength(1)
  expect(nonceOf(browserPrefetch[0])).toBeTruthy()
  expect(await prefetch({ 'next-router-prefetch': '1', rsc: '1' })).toEqual([])
  // The header sources of next.config.ts match without case and the proxy with case: /ADMIN keeps the static CSP.
  for (const path of ['/', '/administrator', '/posts', '/ADMIN/staff']) {
    const csp = cspHeaders((await request.get(path, { maxRedirects: 0 })).headersArray())
    expect(csp, path).toHaveLength(1)
    expect(scriptSrc(csp[0]), path).toMatch(/^'self' 'unsafe-inline'/)
  }
})

test('an inline script without the nonce does not run on an admin page', async ({ page }) => {
  // Written into the HTML as an injection into the rendered page would be. route.fetch() decompresses the body, so
  // the encoding headers go; the CSP header stays.
  await page.route('/admin/sign-in', async (route) => {
    const response = await route.fetch()
    const headers = { ...response.headers() }
    delete headers['content-encoding']
    delete headers['content-length']
    const body = (await response.text()).replace(
      '<head>',
      '<head><script>window.__injected = true</script>',
    )
    await route.fulfill({ response, headers, body })
  })
  const violations: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error' && /Content Security Policy/i.test(message.text())) {
      violations.push(message.text())
    }
  })

  await page.goto('/admin/sign-in')

  // The scripts of Next.js, which carry the nonce, still run: their bootstrap sets window.next. (The form itself
  // waits for Firebase, which Chrome's local network checks refuse to a document that Playwright fulfilled.)
  await page.waitForFunction(() => 'next' in window)
  expect(await page.evaluate(() => (window as { __injected?: boolean }).__injected)).toBeUndefined()
  expect(violations).toHaveLength(1)
  expect(violations[0]).toMatch(/inline script/i)
})
