import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import {
  ChangePasswordActions,
  ChangePasswordContent,
  ChangePasswordLayout,
  ChangePasswordSession,
} from '@/features/identity/account'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('changePassword')
  return { title: t('title') }
}

/** SCR-AUT-02 Required password change, shown alone without the menu. */
export default async function ChangePasswordPage() {
  const t = await getTranslations('changePassword')
  return (
    <ChangePasswordSession>
      <ChangePasswordLayout title={t('title')} actions={<ChangePasswordActions />}>
        <ChangePasswordContent />
      </ChangePasswordLayout>
    </ChangePasswordSession>
  )
}
