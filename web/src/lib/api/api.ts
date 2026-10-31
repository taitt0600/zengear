import { env } from '@/env'
import { currentIdToken } from '@/lib/auth/session'
import { createApiClient, createApiFetch } from './client'
import type { ApiError } from './problems'

/** The one API client of the app (web/CLAUDE.md), with the ID token of the Firebase session. */
export const api = createApiClient({
  baseUrl: env.NEXT_PUBLIC_API_BASE_URL,
  getIdToken: currentIdToken,
})

const send = createApiFetch({ baseUrl: env.NEXT_PUBLIC_API_BASE_URL, getIdToken: currentIdToken })

/**
 * The mutator of the generated client (web/orval.config.ts): every call of the app goes through it. A function
 * declaration, not the const that createApiFetch returns, so that Orval reads its two parameters when it generates.
 */
export function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  return send<T>(url, init)
}

/** The error of a generated call, as Orval types it: an ApiError with the problems of that operation. */
export type ErrorType<E> = ApiError & { readonly problem?: E }
