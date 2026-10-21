import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import { shouldRetry } from './query-provider'

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
