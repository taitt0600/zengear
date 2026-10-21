import { api } from '@/lib/api/api'
import { queryKeys } from '@/lib/api/query-keys'
import { unwrap } from '@/lib/api/problems'
import type { components } from '@/lib/api/schema'

export type CurrentUser = components['schemas']['CurrentUserDto']

/** GET /api/v1/admin/me: the signed-in user, with the permission keys of their roles (AUT-04). */
export const meQuery = {
  queryKey: queryKeys.me,
  queryFn: async ({ signal }: { signal: AbortSignal }): Promise<CurrentUser> =>
    unwrap(await api.GET('/api/v1/admin/me', { signal })),
}
