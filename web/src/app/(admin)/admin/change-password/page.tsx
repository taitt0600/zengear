import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { ChangePasswordScreen } from '@/features/identity/account'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('changePassword')
  return { title: t('title') }
}

/** SCR-AUT-02 Required password change, shown alone without the menu. */
export default function ChangePasswordPage() {
  return <ChangePasswordScreen />
}
