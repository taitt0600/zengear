import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { ScreenPlaceholder } from '@/components/app/screen-placeholder'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin.user')
  return { title: t('account') }
}

/** SCR-ACC-01 My account: built with STF-03; every signed-in staff member may open it. */
export default async function AccountPage() {
  const t = await getTranslations('admin.user')
  return <ScreenPlaceholder title={t('account')} />
}
