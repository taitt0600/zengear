'use client'

import { useQuery } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { RequiredNote } from '@/components/app/required-note'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldGroup } from '@/components/ui/field'
import { Skeleton } from '@/components/ui/skeleton'
import { meQuery, PASSWORD_CHANGED, useStaffSignOut } from '@/features/identity/access'
import { AccountPasswordForm } from './account-password-form'

/**
 * The card "Mật khẩu" of SCR-ACC-01 (its Layout, Password card): a `section` the section navigation reaches
 * (`#password`), named by its h2, with the form on the page; after the change the user is signed out (DEC-094). Its
 * title, description and required note show while GET /me loads, the form once the account's email is known.
 */
export function AccountPasswordSection() {
  const t = useTranslations()
  const signOut = useStaffSignOut()
  const me = useQuery(meQuery)
  return (
    <section id="password" aria-labelledby="password-title" className="scroll-mt-6">
      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id="password-title" tabIndex={-1} className="focus:outline-none">
              {t('account.passwordCard.title')}
            </h2>
          </CardTitle>
          <CardDescription>{t('account.changePasswordNote')}</CardDescription>
          <RequiredNote />
        </CardHeader>
        <CardContent>
          {me.data ? (
            <AccountPasswordForm
              email={me.data.email}
              onChanged={() => signOut(PASSWORD_CHANGED)}
            />
          ) : (
            <AccountPasswordSkeleton />
          )}
        </CardContent>
      </Card>
    </section>
  )
}

/** The form while GET /me loads: its three fields and its button, in place. */
function AccountPasswordSkeleton() {
  return (
    <FieldGroup className="max-w-md">
      {['w-30', 'w-24', 'w-36'].map((width) => (
        <Field key={width}>
          <Skeleton className={`h-4 ${width}`} />
          <Skeleton className="h-9 w-full" />
        </Field>
      ))}
      <Skeleton className="h-9 w-32" />
    </FieldGroup>
  )
}
