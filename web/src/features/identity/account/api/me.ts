import type { LoadedCurrentUser } from '@/features/identity/access'
import { api } from '@/lib/api/api'
import { etagOf } from '@/lib/api/concurrency'
import { unwrap } from '@/lib/api/problems'

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
