import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { AccountScreen } from '@/features/identity/account-screen'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin.user')
  return { title: t('account') }
}

/** SCR-ACC-01 My account (STF-03): every signed-in staff member may open it. */
export default function AccountPage() {
  return <AccountScreen />
}
