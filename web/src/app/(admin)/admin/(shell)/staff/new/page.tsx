import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import { ScreenPlaceholder } from '@/components/app/screen-placeholder'
import { RequirePermission } from '@/features/shell/admin'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('staff.list')
  return { title: t('add') }
}

/**
 * SCR-STF-02 New staff account: built once gap 2 is closed; until then "Thêm nhân viên" opens this page, which also
 * keeps `/admin/staff/new` from reaching SCR-STF-03 as an id.
 */
export default async function NewStaffPage() {
  const t = await getTranslations('staff.list')
  return (
    <RequirePermission permission="staff.create">
      <ScreenPlaceholder title={t('add')} />
    </RequirePermission>
  )
}
