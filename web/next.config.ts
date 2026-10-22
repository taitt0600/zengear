import type { NextConfig } from 'next'
import createNextIntlPlugin from 'next-intl/plugin'
import { parseEnv, publicEnvValues } from './src/env-schema'
import { contentSecurityPolicy, securityHeaders } from './src/lib/security-headers'

const production = process.env.NODE_ENV === 'production'

const nextConfig: NextConfig = {
  // next dev would write and restore an AGENTS.md when it detects a coding agent; web/CLAUDE.md holds the rules.
  agentRules: false,
  // Read when the app builds or serves, not when next typegen loads this file.
  async headers() {
    const csp = contentSecurityPolicy(parseEnv(publicEnvValues(), production), !production)
    return [
      { source: '/:path*', headers: securityHeaders() },
      // /admin and /pos get the CSP with a nonce of src/proxy.ts instead, so that each response has one CSP
      // (DEC-113 (4)). These sources match without case and the proxy matcher with case: (?-i:) keeps the static
      // CSP on /ADMIN, which the proxy does not see.
      {
        source: '/((?!(?-i:admin|pos)(?:/|$)).*)',
        headers: [{ key: 'Content-Security-Policy', value: csp }],
      },
    ]
  },
}

const withNextIntl = createNextIntlPlugin('./src/i18n/request.ts')

export default withNextIntl(nextConfig)
