import { ApiError, hasProblemType } from '@/lib/api/problems'
import type { CurrentUser } from '../api/me'
import { adminRoutes } from './routes'

/**
 * Why a user is signed out when no problem of the API says it, each with its text on the sign-in page (docs/SCREENS.md,
 * SCR-AUT-01, SCR-ADM-01): Firebase disabled the account, or GET /me answered 200 with isStaff false.
 */
export type SignOutReason = 'account-disabled' | 'not-staff'

export const SIGN_OUT_REASONS: readonly SignOutReason[] = ['account-disabled', 'not-staff']

export type MeOutcome =
  | { kind: 'staff'; landing: string }
  | { kind: 'change-password' }
  | { kind: 'sign-out'; reason: SignOutReason }
  // 403 account-disabled or account-not-registered: signed out, with the API's detail on the sign-in page (DEC-117 (4)).
  | { kind: 'account-refused'; error: ApiError }
  | { kind: 'sign-in-again' }
  | { kind: 'error' }

/** The first page of a staff member: the staff list when they may open it, otherwise their account (SCR-AUT-01). */
export function landingPath(permissions: readonly string[]): string {
  return permissions.includes('staff.read') ? adminRoutes.staff : adminRoutes.account
}

/** The code of a Firebase error (`auth/...`), or undefined for any other error. */
export function firebaseCode(error: unknown): string | undefined {
  if (typeof error !== 'object' || error === null || !('code' in error)) return undefined
  return typeof error.code === 'string' && error.code.startsWith('auth/') ? error.code : undefined
}

/** A failed Firebase sign-in with a password (SCR-AUT-01, and the sign-in again of DEC-094), as message keys. */
export type PasswordSignInError =
  | 'signIn.errors.wrongCredentials'
  | 'signIn.errors.tooManyAttempts'
  | 'signIn.errors.accountDisabled'
  | 'signIn.errors.generic'
  | 'problems.network'

// Never which of the two was wrong (SCR-AUT-01).
const WRONG_CREDENTIALS = new Set([
  'auth/invalid-credential',
  'auth/invalid-login-credentials',
  'auth/wrong-password',
  'auth/user-not-found',
  'auth/invalid-email',
])

/** The answer of a failed Firebase sign-in with a password, from its error code. */
export function firebaseError(error: unknown): PasswordSignInError {
  const code = firebaseCode(error) ?? ''
  if (WRONG_CREDENTIALS.has(code)) return 'signIn.errors.wrongCredentials'
  if (code === 'auth/too-many-requests') return 'signIn.errors.tooManyAttempts'
  // ZenGear never disables the Firebase account itself (R-SEC-04 revokes its sessions), but the console can.
  if (code === 'auth/user-disabled') return 'signIn.errors.accountDisabled'
  if (code === 'auth/network-request-failed') return 'problems.network'
  return 'signIn.errors.generic'
}

// The ID token cannot be refreshed: the session ended, revoked by a password change (DEC-094) or a disable (R-SEC-04).
const SESSION_ENDED = new Set([
  'auth/user-token-expired',
  'auth/invalid-user-token',
  'auth/user-not-found',
])

/**
 * What a signed-in user's API answer means for the session (docs/SCREENS.md, Conventions for every screen): the one
 * mapping used by the sign-in page, the admin layout and the error handler of every query. The Firebase errors come
 * from getIdToken() in the API client. Returns undefined for an error that is not about the session, which the screen
 * shows itself.
 */
export function sessionOutcome(error: unknown): MeOutcome | undefined {
  const code = firebaseCode(error)
  if (code && SESSION_ENDED.has(code)) return { kind: 'sign-in-again' }
  if (code === 'auth/user-disabled') return { kind: 'sign-out', reason: 'account-disabled' }
  if (!(error instanceof ApiError)) return undefined
  if (error.status === 401) return { kind: 'sign-in-again' }
  if (error.status !== 403) return undefined
  if (
    hasProblemType(error.problem, 'account-disabled') ||
    hasProblemType(error.problem, 'account-not-registered')
  ) {
    return { kind: 'account-refused', error }
  }
  if (hasProblemType(error.problem, 'password-change-required')) return { kind: 'change-password' }
  return undefined
}

/** The outcome of GET /api/v1/admin/me for a user signed in to Firebase (SCR-AUT-01 step 2, SCR-ADM-01 guard). */
export function meOutcome(result: { me: CurrentUser } | { error: unknown }): MeOutcome {
  if ('error' in result) {
    return sessionOutcome(result.error) ?? { kind: 'error' }
  }
  const { me } = result
  if (!me.isStaff) return { kind: 'sign-out', reason: 'not-staff' }
  if (me.mustChangePassword) return { kind: 'change-password' }
  return { kind: 'staff', landing: landingPath(me.permissions) }
}
