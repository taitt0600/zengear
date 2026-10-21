import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { ScreenPlaceholder } from '@/components/app/screen-placeholder'
import { RequirePermission } from '@/features/identity/no-permission'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin.menu')
  return { title: t('staff') }
}

/** SCR-STF-01 Staff list: built with STF-05; until then the menu entry opens this page. */
export default async function StaffPage() {
  const t = await getTranslations('admin.menu')
  return (
    <RequirePermission permission="staff.read">
      <ScreenPlaceholder title={t('staff')} />
    </RequirePermission>
  )
}
