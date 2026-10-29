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
