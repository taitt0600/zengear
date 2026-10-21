import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/api/problems'
import type { CurrentUser } from './me'
import { landingPath, meOutcome, sessionOutcome } from './me-outcome'

const staff: CurrentUser = {
  id: '0199a0f3-0000-7000-8000-000000000001',
  name: 'Chủ cửa hàng',
  email: 'owner@zengear.test',
  isStaff: true,
  mustChangePassword: false,
  permissions: ['business_location.update', 'staff.read'],
}

const problem = (status: number, name: string) =>
  new ApiError(status, { type: `/problems/${name}`, status })

describe('meOutcome', () => {
  it('lets a staff member in, to the landing page of their permissions', () => {
    expect(meOutcome({ me: staff })).toEqual({ kind: 'staff', landing: '/admin/staff' })
  })

  it('signs out an account that is not a staff account, before anything else', () => {
    expect(meOutcome({ me: { ...staff, isStaff: false, mustChangePassword: true } })).toEqual({
      kind: 'sign-out',
      reason: 'not-staff',
    })
  })

  it('sends a staff member with the default password to the password change', () => {
    expect(meOutcome({ me: { ...staff, mustChangePassword: true } })).toEqual({
      kind: 'change-password',
    })
  })

  it('maps the session problems of the API', () => {
    expect(meOutcome({ error: problem(403, 'account-disabled') })).toEqual({
      kind: 'sign-out',
      reason: 'account-disabled',
    })
    expect(meOutcome({ error: problem(403, 'account-not-registered') })).toEqual({
      kind: 'sign-out',
      reason: 'account-not-registered',
    })
    expect(meOutcome({ error: problem(403, 'password-change-required') })).toEqual({
      kind: 'change-password',
    })
    expect(meOutcome({ error: new ApiError(401) })).toEqual({ kind: 'sign-in-again' })
  })

  it('reports other answers as errors, telling a network failure apart', () => {
    expect(meOutcome({ error: new ApiError(500) })).toEqual({ kind: 'error', network: false })
    expect(meOutcome({ error: problem(403, 'permission-denied') })).toEqual({
      kind: 'error',
      network: false,
    })
    expect(meOutcome({ error: new TypeError('Failed to fetch') })).toEqual({
      kind: 'error',
      network: true,
    })
  })
})

describe('a token Firebase can no longer refresh', () => {
  const firebaseError = (code: string) => Object.assign(new Error(code), { code })

  it.each(['auth/user-token-expired', 'auth/invalid-user-token', 'auth/user-not-found'])(
    '%s asks to sign in again',
    (code) => {
      expect(meOutcome({ error: firebaseError(code) })).toEqual({ kind: 'sign-in-again' })
    },
  )

  it('auth/user-disabled signs out as a disabled account', () => {
    expect(meOutcome({ error: firebaseError('auth/user-disabled') })).toEqual({
      kind: 'sign-out',
      reason: 'account-disabled',
    })
  })

  it('auth/network-request-failed is a network error, another code is not', () => {
    expect(meOutcome({ error: firebaseError('auth/network-request-failed') })).toEqual({
      kind: 'error',
      network: true,
    })
    expect(meOutcome({ error: firebaseError('auth/internal-error') })).toEqual({
      kind: 'error',
      network: false,
    })
  })
})

describe('sessionOutcome', () => {
  it('leaves the errors that are not about the session to the screen', () => {
    expect(sessionOutcome(problem(403, 'permission-denied'))).toBeUndefined()
    expect(sessionOutcome(problem(409, 'last-active-admin'))).toBeUndefined()
    expect(sessionOutcome(new TypeError('Failed to fetch'))).toBeUndefined()
  })
})

describe('landingPath', () => {
  it('is the staff list with staff.read, otherwise the account', () => {
    expect(landingPath(['staff.read'])).toBe('/admin/staff')
    expect(landingPath(['business_location.update'])).toBe('/admin/account')
    expect(landingPath([])).toBe('/admin/account')
  })
})
