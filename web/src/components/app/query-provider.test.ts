import { afterEach, describe, expect, it, vi } from 'vitest'
import { onApiError } from '@/lib/api/api-errors'
import { ApiError } from '@/lib/api/problems'
import { createQueryClient, shouldRetry } from './query-provider'

describe('shouldRetry', () => {
  it('never retries a 4xx answer', () => {
    expect(shouldRetry(0, new ApiError(403))).toBe(false)
    expect(shouldRetry(0, new ApiError(404))).toBe(false)
  })

  it('retries a 5xx or a network error twice', () => {
    expect(shouldRetry(0, new ApiError(503))).toBe(true)
    expect(shouldRetry(1, new TypeError('Failed to fetch'))).toBe(true)
    expect(shouldRetry(2, new ApiError(503))).toBe(false)
    expect(shouldRetry(2, new TypeError('Failed to fetch'))).toBe(false)
  })
})

describe('createQueryClient', () => {
  const stop: (() => void)[] = []
  afterEach(() => stop.splice(0).forEach((unsubscribe) => unsubscribe()))

  it('reports the failed queries and mutations to the admin layout, but not GET /me', async () => {
    const reported = vi.fn()
    stop.push(onApiError(reported))
    const client = createQueryClient()
    const denied = new ApiError(403, { type: '/problems/permission-denied' })

    await client
      .query({ queryKey: ['staff'], queryFn: () => Promise.reject(denied) })
      .catch(() => {})
    await client
      .query({ queryKey: ['me'], queryFn: () => Promise.reject(new ApiError(401)) })
      .catch(() => {})
    await client
      .getMutationCache()
      .build(client, { mutationFn: () => Promise.reject(new ApiError(409)) })
      .execute(undefined)
      .catch(() => {})

    expect(reported.mock.calls.map(([error]) => (error as ApiError).status)).toEqual([403, 409])
  })
})
