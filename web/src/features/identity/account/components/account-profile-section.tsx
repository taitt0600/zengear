'use client'

import { useQuery } from '@tanstack/react-query'
import { meQuery, type LoadedCurrentUser } from '@/features/identity/access'
import { useAccountForm } from '../hooks/use-account-form'
import { AccountProfileFields, AccountProfileSkeleton } from './account-profile-fields'

/**
 * The profile of SCR-ACC-01 My account (UC-007, STF-03): the display name and the email read-only, loaded from
 * GET /me, the query the admin layout already holds.
 */
export function AccountProfileSection() {
  const me = useQuery(meQuery)
  return me.data ? <ProfileForm loaded={me.data} /> : <AccountProfileSkeleton />
}

// Mounted once GET /me answered: the form holds the version it loaded from then on.
function ProfileForm({ loaded }: { loaded: LoadedCurrentUser }) {
  const form = useAccountForm(loaded)
  return <AccountProfileFields {...form} email={loaded.email} />
}
