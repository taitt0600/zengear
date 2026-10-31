import { describe, expect, it, vi } from 'vitest'
import { meQuery } from './me'

const mutator = vi.hoisted(() => ({ apiFetch: vi.fn() }))
vi.mock('@/lib/api/api', () => mutator)

const me = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: ['staff.read'],
}

describe('meQuery', () => {
  it('loads GET /me with the signal of the query, and keeps its ETag', async () => {
    mutator.apiFetch.mockResolvedValue({
      data: me,
      status: 200,
      headers: new Headers({ ETag: 'W/"42"' }),
    })
    const signal = new AbortController().signal

    expect(await meQuery.queryFn({ signal })).toEqual({ ...me, etag: 'W/"42"' })

    const [url, init] = mutator.apiFetch.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/v1/admin/me')
    expect(init).toMatchObject({ method: 'GET', signal })
  })

  it('leaves its failures to the session, not to the query cache', () => {
    expect(meQuery.meta).toEqual({ reportErrors: false })
  })
})
