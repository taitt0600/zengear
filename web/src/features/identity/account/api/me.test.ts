import { beforeEach, describe, expect, it, vi } from 'vitest'
import { changeMyPassword, updateMe } from './me'

const mutator = vi.hoisted(() => ({ apiFetch: vi.fn() }))
vi.mock('@/lib/api/api', () => mutator)

const account = {
  id: '0199a0f3-0000-7000-8000-000000000009',
  name: 'Tên mới',
  email: 'tuvan@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: [],
}

// The URL and the init the generated call gave the mutator.
function sent() {
  expect(mutator.apiFetch).toHaveBeenCalledOnce()
  const [url, init] = mutator.apiFetch.mock.calls[0] as [string, RequestInit]
  return { url, init, headers: new Headers(init.headers) }
}

beforeEach(() => mutator.apiFetch.mockReset())

describe('updateMe', () => {
  it('sends the name with the version it edits, and returns the account with its new ETag', async () => {
    mutator.apiFetch.mockResolvedValue({
      data: account,
      status: 200,
      headers: new Headers({ ETag: '"v2"' }),
    })

    expect(await updateMe('Tên mới', '"v1"')).toEqual({ ...account, etag: '"v2"' })

    const { url, init, headers } = sent()
    expect(url).toBe('/api/v1/admin/me')
    expect(init.method).toBe('PATCH')
    expect(headers.get('If-Match')).toBe('"v1"')
    expect(JSON.parse(init.body as string)).toEqual({ name: 'Tên mới' })
  })
})

describe('changeMyPassword', () => {
  it('posts the new password', async () => {
    mutator.apiFetch.mockResolvedValue({ data: undefined, status: 204, headers: new Headers() })

    await changeMyPassword('mot mat khau moi dai')

    const { url, init } = sent()
    expect(url).toBe('/api/v1/admin/me/change-password')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({ newPassword: 'mot mat khau moi dai' })
  })
})
