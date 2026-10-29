import { describe, expect, it } from 'vitest'
import { safeNext } from './routes'

describe('safeNext', () => {
  it.each([
    '/admin',
    '/admin/staff',
    '/admin/staff?page=2&sort=-joinedDate',
    '/admin/settings/business-location#hours',
  ])('keeps the admin path %s', (next) => {
    expect(safeNext(next)).toBe(next)
  })

  it.each([
    null,
    '',
    'https://evil.example/admin',
    '//evil.example/admin',
    '/admin//evil.example',
    '/admin/\\evil.example',
    '/administrator',
    '/shop',
    'admin/staff',
    'javascript:alert(1)',
    '/admin/sign-in',
    '/admin/sign-in?next=/admin/staff',
  ])('ignores %s', (next) => {
    expect(safeNext(next)).toBeUndefined()
  })
})
