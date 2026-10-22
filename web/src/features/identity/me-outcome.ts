import { ApiError, problemName } from '@/lib/api/problems'
import type { CurrentUser } from './me'
import { adminRoutes } from './routes'

/** Why a user is signed out; each reason has its text on the sign-in page (docs/SCREENS.md, SCR-AUT-01, SCR-ADM-01). */
export type SignOutReason = 'account-disabled' | 'account-not-registered' | 'not-staff'

export const SIGN_OUT_REASONS: readonly SignOutReason[] = [
  'account-disabled',
  'account-not-registered',
  'not-staff',
]

export type MeOutcome =
  | { kind: 'staff'; landing: string }
  | { kind: 'change-password' }
  | { kind: 'sign-out'; reason: SignOutReason }
  | { kind: 'sign-in-again' }
  | { kind: 'error'; network: boolean }

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
  'wrongCredentials' | 'tooManyAttempts' | 'accountDisabled' | 'network' | 'generic'

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
  if (WRONG_CREDENTIALS.has(code)) return 'wrongCredentials'
  if (code === 'auth/too-many-requests') return 'tooManyAttempts'
  // ZenGear never disables the Firebase account itself (R-SEC-04 revokes its sessions), but the console can.
  if (code === 'auth/user-disabled') return 'accountDisabled'
  if (code === 'auth/network-request-failed') return 'network'
  return 'generic'
}

// The ID token cannot be refreshed: the session ended, revoked by a password change (DEC-094) or a disable (R-SEC-04).
const SESSION_ENDED = new Set([
  'auth/user-token-expired',
  'auth/invalid-user-token',
  'auth/user-not-found',
])

/** Whether an error means the API or Firebase could not be reached. */
export function isNetworkError(error: unknown): boolean {
  return error instanceof TypeError || firebaseCode(error) === 'auth/network-request-failed'
}

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
  switch (problemName(error.problem)) {
    case 'account-disabled':
      return { kind: 'sign-out', reason: 'account-disabled' }
    case 'account-not-registered':
      return { kind: 'sign-out', reason: 'account-not-registered' }
    case 'password-change-required':
      return { kind: 'change-password' }
    default:
      return undefined
  }
}

/** The outcome of GET /api/v1/admin/me for a user signed in to Firebase (SCR-AUT-01 step 2, SCR-ADM-01 guard). */
export function meOutcome(result: { me: CurrentUser } | { error: unknown }): MeOutcome {
  if ('error' in result) {
    return sessionOutcome(result.error) ?? { kind: 'error', network: isNetworkError(result.error) }
  }
  const { me } = result
  if (!me.isStaff) return { kind: 'sign-out', reason: 'not-staff' }
  if (me.mustChangePassword) return { kind: 'change-password' }
  return { kind: 'staff', landing: landingPath(me.permissions) }
}
