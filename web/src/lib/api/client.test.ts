import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from './client'
import { etagOf, newIdempotencyKey } from './concurrency'

const me = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: ['staff.read'],
}

function fakeFetch() {
  return vi.fn<typeof fetch>(
    async () =>
      new Response(JSON.stringify(me), {
        status: 200,
        headers: { 'Content-Type': 'application/json', ETag: 'W/"42"' },
      }),
  )
}

// openapi-fetch calls fetch with one Request that holds every header.
function sentRequest(fetch: ReturnType<typeof fakeFetch>): Request {
  expect(fetch).toHaveBeenCalledOnce()
  const [request] = fetch.mock.calls[0]
  expect(request).toBeInstanceOf(Request)
  return request as Request
}

describe('createApiClient', () => {
  it('sends the Firebase ID token as a Bearer token', async () => {
    const fetch = fakeFetch()
    const client = createApiClient({
      baseUrl: 'http://api.test',
      getIdToken: async () => 'id-token',
      fetch,
    })

    const { data } = await client.GET('/api/v1/admin/me')

    const request = sentRequest(fetch)
    expect(request.url).toBe('http://api.test/api/v1/admin/me')
    expect(request.headers.get('Authorization')).toBe('Bearer id-token')
    expect(data).toEqual(me)
  })

  it('sends no Authorization header when nobody is signed in', async () => {
    const fetch = fakeFetch()
    const client = createApiClient({
      baseUrl: 'http://api.test',
      getIdToken: async () => null,
      fetch,
    })

    await client.GET('/api/v1/admin/me')

    expect(sentRequest(fetch).headers.has('Authorization')).toBe(false)
  })

  it('never sends the token to a shop endpoint, even when signed in', async () => {
    const fetch = fakeFetch()
    const getIdToken = vi.fn(async () => 'id-token')
    const client = createApiClient({ baseUrl: 'http://api.test', getIdToken, fetch })

    await client.GET('/api/v1/shop/business-locations')

    expect(sentRequest(fetch).headers.has('Authorization')).toBe(false)
    expect(getIdToken).not.toHaveBeenCalled()
  })

  it('sends the ETag of the loaded resource back as If-Match', async () => {
    const fetch = fakeFetch()
    const client = createApiClient({
      baseUrl: 'http://api.test',
      getIdToken: async () => 't',
      fetch,
    })
    const loaded = await client.GET('/api/v1/admin/me')
    fetch.mockClear()

    await client.PATCH('/api/v1/admin/me', {
      params: { header: { 'If-Match': etagOf(loaded.response) } },
      body: { name: 'Chủ cửa hàng' },
    })

    expect(sentRequest(fetch).headers.get('If-Match')).toBe('W/"42"')
  })
})

describe('etagOf', () => {
  it('is undefined when the response has no ETag', () => {
    expect(etagOf(new Response(null))).toBeUndefined()
  })
})

describe('newIdempotencyKey', () => {
  it('is a new UUID for each attempt', () => {
    const first = newIdempotencyKey()
    expect(first).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
    expect(newIdempotencyKey()).not.toBe(first)
  })
})
