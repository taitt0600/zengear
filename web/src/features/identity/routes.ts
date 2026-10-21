// The routes of the admin part (docs/SCREENS.md, Screen list).
export const adminRoutes = {
  signIn: '/admin/sign-in',
  changePassword: '/admin/change-password',
  account: '/admin/account',
  staff: '/admin/staff',
  businessLocation: '/admin/settings/business-location',
} as const

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
