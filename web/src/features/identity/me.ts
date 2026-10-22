import { api } from '@/lib/api/api'
import { etagOf } from '@/lib/api/concurrency'
import { queryKeys } from '@/lib/api/query-keys'
import { unwrap } from '@/lib/api/problems'
import type { components } from '@/lib/api/schema'

export type CurrentUser = components['schemas']['CurrentUserDto']

/** The signed-in user as GET /me returned it, with its ETag, which PATCH /me sends back as If-Match. */
export type LoadedCurrentUser = CurrentUser & { etag?: string }

/** GET /api/v1/admin/me: the signed-in user, with the permission keys of their roles (AUT-04). */
export const meQuery = {
  queryKey: queryKeys.me,
  queryFn: async ({ signal }: { signal: AbortSignal }): Promise<LoadedCurrentUser> => {
    const result = await api.GET('/api/v1/admin/me', { signal })
    return { ...unwrap(result), etag: etagOf(result.response) }
  },
}

/**
 * PATCH /api/v1/admin/me: the display name (STF-03, R-USR-04), for the version `etag` of the account. Returns the
 * account as saved, with its new ETag.
 */
export async function updateMe(name: string, etag: string): Promise<LoadedCurrentUser> {
  const result = await api.PATCH('/api/v1/admin/me', {
    params: { header: { 'If-Match': etag } },
    body: { name },
  })
  return { ...unwrap(result), etag: etagOf(result.response) }
}

/**
 * POST /api/v1/admin/me/change-password (DEC-094): the API checks R-USR-11, changes the password, clears
 * must_change_password and revokes every session. The password is only sent, never kept.
 */
export async function changeMyPassword(newPassword: string): Promise<void> {
  unwrap(await api.POST('/api/v1/admin/me/change-password', { body: { newPassword } }))
}
