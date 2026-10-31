import { describe, expect, it, vi } from 'vitest'
import { createApiClient, createApiFetch, type ApiResponse } from './client'
import { etagOf, newIdempotencyKey } from './concurrency'
import { ApiError } from './problems'

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

describe('createApiFetch', () => {
  function json(body: unknown, status: number, contentType = 'application/json') {
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
  }

  function setup(answer: Response | Error = json(me, 200), token: string | null = 'id-token') {
    const fetch = vi.fn<typeof globalThis.fetch>(async () => {
      if (answer instanceof Error) throw answer
      return answer
    })
    const getIdToken = vi.fn(async () => token)
    const apiFetch = createApiFetch({ baseUrl: 'http://api.test', getIdToken, fetch })
    return { fetch, getIdToken, apiFetch }
  }

  // The mutator calls fetch with the URL and the init it built.
  function sent(fetch: ReturnType<typeof setup>['fetch']) {
    expect(fetch).toHaveBeenCalledOnce()
    const [url, init] = fetch.mock.calls[0]
    return { url, init: init ?? {}, headers: new Headers(init?.headers) }
  }

  it('sends the Firebase ID token as a Bearer token', async () => {
    const { fetch, apiFetch } = setup()

    await apiFetch('/api/v1/admin/me', { method: 'GET' })

    const { url, init, headers } = sent(fetch)
    expect(url).toBe('http://api.test/api/v1/admin/me')
    expect(init.method).toBe('GET')
    expect(headers.get('Authorization')).toBe('Bearer id-token')
  })

  it('sends no Authorization header when nobody is signed in', async () => {
    const { fetch, apiFetch } = setup(json(me, 200), null)

    await apiFetch('/api/v1/admin/me')

    expect(sent(fetch).headers.has('Authorization')).toBe(false)
  })

  it('never sends the token to a shop endpoint, even when signed in', async () => {
    const { fetch, getIdToken, apiFetch } = setup()

    await apiFetch('/api/v1/shop/business-locations?page=1')

    expect(sent(fetch).headers.has('Authorization')).toBe(false)
    expect(getIdToken).not.toHaveBeenCalled()
  })

  it('sends the If-Match header it is given, beside the token', async () => {
    const { fetch, apiFetch } = setup()

    await apiFetch('/api/v1/admin/me', {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json', 'If-Match': 'W/"42"' },
      body: JSON.stringify({ name: 'Chủ cửa hàng' }),
    })

    const { init, headers } = sent(fetch)
    expect(headers.get('If-Match')).toBe('W/"42"')
    expect(headers.get('Authorization')).toBe('Bearer id-token')
    expect(init.body).toBe('{"name":"Chủ cửa hàng"}')
  })

  it('passes the abort signal of the query to fetch', async () => {
    const { fetch, apiFetch } = setup()
    const signal = new AbortController().signal

    await apiFetch('/api/v1/admin/me', { signal })

    expect(sent(fetch).init.signal).toBe(signal)
  })

  it('returns the data of a success', async () => {
    const { apiFetch } = setup()

    expect(await apiFetch('/api/v1/admin/me')).toMatchObject({ data: me, status: 200 })
  })

  it('exposes the headers of a success, for its ETag', async () => {
    const { apiFetch } = setup(
      new Response(JSON.stringify(me), {
        status: 200,
        headers: { 'Content-Type': 'application/json', ETag: 'W/"42"' },
      }),
    )

    const { headers } = await apiFetch<ApiResponse>('/api/v1/admin/me')

    expect(headers.get('ETag')).toBe('W/"42"')
  })

  it('returns no data for a 204', async () => {
    const { apiFetch } = setup(new Response(null, { status: 204 }))

    expect(await apiFetch('/api/v1/admin/me/change-password')).toMatchObject({
      data: undefined,
      status: 204,
    })
  })

  it('throws an ApiError with the status and the problem', async () => {
    const problem = { type: '/problems/permission-denied', status: 403, detail: 'Không có quyền.' }
    const { apiFetch } = setup(json(problem, 403, 'application/problem+json'))

    const error = await apiFetch('/api/v1/admin/staff').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 403, problem })
  })

  it('keeps no problem when the body was not one', async () => {
    const { apiFetch } = setup(
      new Response('<html>Bad Gateway</html>', {
        status: 502,
        headers: { 'Content-Type': 'text/html' },
      }),
    )

    const error = await apiFetch('/api/v1/admin/staff').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 502, problem: undefined })
  })

  it('keeps no problem when a JSON body does not parse', async () => {
    const { apiFetch } = setup(
      new Response('{"type":', { status: 502, headers: { 'Content-Type': 'application/json' } }),
    )

    const error = await apiFetch('/api/v1/admin/staff').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 502, problem: undefined })
  })

  it('throws a failed getIdToken as it is, without calling the API', async () => {
    const failure = { code: 'auth/network-request-failed' }
    const fetch = vi.fn<typeof globalThis.fetch>()
    const apiFetch = createApiFetch({
      baseUrl: 'http://api.test',
      getIdToken: () => Promise.reject(failure),
      fetch,
    })

    await expect(apiFetch('/api/v1/admin/me')).rejects.toBe(failure)
    expect(fetch).not.toHaveBeenCalled()
  })

  it('throws a failed fetch as it is', async () => {
    const failure = new TypeError('Failed to fetch')
    const { apiFetch } = setup(failure)

    await expect(apiFetch('/api/v1/admin/me')).rejects.toBe(failure)
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
