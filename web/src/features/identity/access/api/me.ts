import { etagOf } from '@/lib/api/concurrency'
import { getApiV1AdminMe, getGetApiV1AdminMeQueryKey } from '@/lib/api/generated/me/me'
import type { CurrentUserDto } from '@/lib/api/generated/model'

export type CurrentUser = CurrentUserDto

/** The signed-in user as GET /me returned it, with its ETag, which PATCH /me sends back as If-Match. */
export type LoadedCurrentUser = CurrentUser & { etag?: string }

/**
 * GET /api/v1/admin/me: the signed-in user, with the permission keys of their roles (AUT-04). The cache holds the
 * user with its ETag under the generated key, so read it only through this query, never through the generated hook.
 * Its failures are the session's own: the layout reads them from this query, so the query cache does not report them.
 */
export const meQuery = {
  queryKey: getGetApiV1AdminMeQueryKey(),
  queryFn: async ({ signal }: { signal: AbortSignal }): Promise<LoadedCurrentUser> => {
    const { data, headers } = await getApiV1AdminMe({ signal })
    return { ...data, etag: etagOf(headers) }
  },
  meta: { reportErrors: false },
}
