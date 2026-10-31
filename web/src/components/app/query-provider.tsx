'use client'

import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { reportApiError } from '@/lib/api/api-errors'
import { ApiError } from '@/lib/api/problems'

/** Retries a network error or a 5xx twice; a 4xx is the answer and is never retried. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status < 500) return false
  return failureCount < 2
}

export function createQueryClient(): QueryClient {
  return new QueryClient({
    // Every failed call goes to the admin layout's rules (401, 403), but a query whose own reader handles its
    // failures, as the layout reads GET /me's result, says so with meta.reportErrors false.
    queryCache: new QueryCache({
      onError: (error, query) => {
        if (query.meta?.reportErrors !== false) reportApiError(error)
      },
    }),
    mutationCache: new MutationCache({ onError: (error) => reportApiError(error) }),
    defaultOptions: { queries: { retry: shouldRetry } },
  })
}

/** Server state lives in TanStack Query (web/CLAUDE.md); one client per browser session. */
export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(createQueryClient)
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}
