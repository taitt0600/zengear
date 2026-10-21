'use client'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { ApiError } from '@/lib/api/problems'

/** Retries a network error or a 5xx twice; a 4xx is the answer and is never retried. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status < 500) return false
  return failureCount < 2
}

/** Server state lives in TanStack Query (web/CLAUDE.md); one client per browser session. */
export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: shouldRetry } } }),
  )
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}
