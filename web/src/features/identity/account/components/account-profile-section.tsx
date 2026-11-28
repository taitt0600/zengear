'use client'

import { useQuery } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { ProfilePhoto } from '@/components/app/profile-photo'
import { RequiredNote } from '@/components/app/required-note'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { meQuery, type LoadedCurrentUser } from '@/features/identity/access'
import { useAccountForm } from '../hooks/use-account-form'
import { AccountDetails } from './account-details'
import { AccountProfileFields, AccountProfileSkeleton } from './account-profile-fields'

/**
 * The card "Thông tin tài khoản" of SCR-ACC-01 (UC-007, STF-03; its Layout, Profile card): a `section` the section
 * navigation reaches (`#profile`), named by its h2, which shows with the description and the required note while GET
 * /me loads (the query the admin layout already holds). Its content is a `@container`: from `@xl` the information on
 * the left and the photo block (the initials until gap 8) in a column of 200px on the right; below it the photo above
 * the information, centred. The photo block comes first in the DOM, as the narrow layout shows it.
 */
export function AccountProfileSection() {
  const t = useTranslations('account.profile')
  const me = useQuery(meQuery).data
  return (
    <section id="profile" aria-labelledby="profile-title" className="scroll-mt-6">
      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id="profile-title" tabIndex={-1} className="focus:outline-none">
              {t('title')}
            </h2>
          </CardTitle>
          <CardDescription>{t('description')}</CardDescription>
          <RequiredNote />
        </CardHeader>
        <CardContent className="@container">
          <div className="grid gap-6 @xl:grid-cols-[minmax(0,1fr)_200px] @xl:gap-8">
            <div className="flex justify-center @xl:col-start-2 @xl:row-start-1 @xl:border-l @xl:pt-1 @xl:pl-8">
              {me ? (
                <ProfilePhoto name={me.name} />
              ) : (
                <Skeleton className="size-24 rounded-full @xl:size-40" />
              )}
            </div>
            <div className="flex min-w-0 flex-col gap-5 @xl:row-start-1">
              {me ? <ProfileForm loaded={me} /> : <AccountProfileSkeleton />}
            </div>
          </div>
        </CardContent>
      </Card>
    </section>
  )
}

// Mounted once GET /me answered: the form holds the version it loaded from then on.
function ProfileForm({ loaded }: { loaded: LoadedCurrentUser }) {
  const form = useAccountForm(loaded)
  return (
    <>
      <AccountProfileFields {...form} />
      <AccountDetails email={loaded.email} />
    </>
  )
}
