import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { Suspense } from 'react'
import { SignInForm, SignInSkeleton } from '@/features/identity/sign-in'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('signIn')
  return { title: t('title') }
}

/** SCR-AUT-01 Staff sign-in. */
export default function SignInPage() {
  return (
    <main className="flex flex-1 items-center justify-center p-4">
      {/* The form reads ?next= and ?reason= from the URL. */}
      <Suspense fallback={<SignInSkeleton />}>
        <SignInForm />
      </Suspense>
    </main>
  )
}
