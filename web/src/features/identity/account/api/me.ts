import type { LoadedCurrentUser } from '@/features/identity/access'
import { etagOf } from '@/lib/api/concurrency'
import {
  getPostApiV1AdminMeChangePasswordMutationKey,
  patchApiV1AdminMe,
  postApiV1AdminMeChangePassword,
} from '@/lib/api/generated/me/me'

/**
 * PATCH /api/v1/admin/me: the display name (STF-03, R-USR-04), for the version `etag` of the account. Returns the
 * account as saved, with its new ETag.
 */
export async function updateMe(name: string, etag: string): Promise<LoadedCurrentUser> {
  const { data, headers } = await patchApiV1AdminMe({ name }, { 'If-Match': etag })
  return { ...data, etag: etagOf(headers) }
}

/** The key of the password change while it is sent, which keeps the dialog of SCR-ACC-01 open. */
export const changePasswordKey = getPostApiV1AdminMeChangePasswordMutationKey()

/**
 * POST /api/v1/admin/me/change-password (DEC-094): the API checks R-USR-11, changes the password, clears
 * must_change_password and revokes every session. The password is only sent, never kept: not through the generated
 * mutation hook, whose variables the mutation cache would keep.
 */
export async function changeMyPassword(newPassword: string): Promise<void> {
  await postApiV1AdminMeChangePassword({ newPassword })
}
