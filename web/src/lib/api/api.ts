import { env } from '@/env'
import { currentIdToken } from '@/lib/auth/session'
import { createApiClient } from './client'

/** The one API client of the app (web/CLAUDE.md), with the ID token of the Firebase session. */
export const api = createApiClient({
  baseUrl: env.NEXT_PUBLIC_API_BASE_URL,
  getIdToken: currentIdToken,
})
