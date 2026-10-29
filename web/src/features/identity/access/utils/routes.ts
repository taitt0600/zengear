// The routes of the admin part (docs/SCREENS.md, Screen list).
export const adminRoutes = {
  signIn: '/admin/sign-in',
  changePassword: '/admin/change-password',
  account: '/admin/account',
  staff: '/admin/staff',
  businessLocation: '/admin/settings/business-location',
} as const

/**
 * The reason of the sign-out after a password change (DEC-094 revokes every session). Not one of the
 * SIGN_OUT_REASONS of me-outcome.ts: the sign-in page shows it as a notice after a success, not as an error.
 */
export const PASSWORD_CHANGED = 'password-changed'

/** The sign-in page, with the path to return to or the reason of a sign-out. */
export function signInPath(query: { next?: string; reason?: string } = {}): string {
  const search = new URLSearchParams()
  if (query.next) search.set('next', query.next)
  if (query.reason) search.set('reason', query.reason)
  const text = search.toString()
  return text ? `${adminRoutes.signIn}?${text}` : adminRoutes.signIn
}

/**
 * The `next` path of the sign-in page when it is safe to go to: an admin path of this site, not the sign-in page
 * itself. Anything else (another site, `//host`, a backslash) is ignored, so the parameter cannot redirect away.
 */
export function safeNext(next: string | null | undefined): string | undefined {
  if (!next || !/^\/admin(?:[/?#]|$)/.test(next) || /[\\]|\/\//.test(next)) return undefined
  const path = next.split(/[?#]/)[0]
  if (path === adminRoutes.signIn || path.startsWith(`${adminRoutes.signIn}/`)) return undefined
  return next
}
