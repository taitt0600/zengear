import type { NextConfig } from 'next'
import createNextIntlPlugin from 'next-intl/plugin'
import { parseEnv, publicEnvValues } from './src/env-schema'
import { securityHeaders } from './src/lib/security-headers'

const production = process.env.NODE_ENV === 'production'

const nextConfig: NextConfig = {
  // next dev would write and restore an AGENTS.md when it detects a coding agent; web/CLAUDE.md holds the rules.
  agentRules: false,
  // Read when the app builds or serves, not when next typegen loads this file.
  async headers() {
    return [
      {
        source: '/:path*',
        headers: securityHeaders(parseEnv(publicEnvValues(), production), !production),
      },
    ]
  },
}

const withNextIntl = createNextIntlPlugin('./src/i18n/request.ts')

export default withNextIntl(nextConfig)
