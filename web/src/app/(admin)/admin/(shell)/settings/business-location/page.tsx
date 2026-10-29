import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { ScreenPlaceholder } from '@/components/app/screen-placeholder'
import { RequirePermission } from '@/features/shell/admin'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin.menu')
  return { title: t('businessLocation') }
}

/** SCR-LOC-01 Business location: built with LOC-01 once gap 3 is closed; until then the menu entry opens this page. */
export default async function BusinessLocationPage() {
  const t = await getTranslations('admin.menu')
  return (
    <RequirePermission permission="business_location.update">
      <ScreenPlaceholder title={t('businessLocation')} />
    </RequirePermission>
  )
}
