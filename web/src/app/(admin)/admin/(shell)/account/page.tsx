import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import {
  AccountHeadingSection,
  AccountLayout,
  AccountNavSection,
  AccountPasswordSection,
  AccountProfileSection,
} from '@/features/identity/account'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin.user')
  return { title: t('account') }
}

/** SCR-ACC-01 My account (STF-03): every signed-in staff member may open it. */
export default function AccountPage() {
  return (
    <AccountLayout
      heading={<AccountHeadingSection />}
      nav={<AccountNavSection />}
      profile={<AccountProfileSection />}
      password={<AccountPasswordSection />}
    />
  )
}
