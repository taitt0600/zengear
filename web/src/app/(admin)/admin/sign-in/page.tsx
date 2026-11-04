import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { Suspense } from 'react'
import { SignInBrand, SignInForm, SignInLayout, SignInSkeleton } from '@/features/identity/sign-in'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('signIn')
  return { title: t('title') }
}

/** SCR-AUT-01 Staff sign-in. */
export default function SignInPage() {
  return (
    <SignInLayout brand={<SignInBrand />}>
      {/* The form reads ?next= and ?reason= from the URL. */}
      <Suspense fallback={<SignInSkeleton />}>
        <SignInForm />
      </Suspense>
    </SignInLayout>
  )
}
