import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import {
  StaffListFiltersSection,
  StaffListHeaderSection,
  StaffListLayout,
  StaffListProvider,
  StaffListTableSection,
} from '@/features/identity/staff-accounts'
import { RequirePermission } from '@/features/shell/admin'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('admin.menu')
  return { title: t('staff') }
}

/** SCR-STF-01 Staff list (STF-05): the staff accounts, for a user who holds staff.read. */
export default function StaffPage() {
  return (
    <RequirePermission permission="staff.read">
      <StaffListProvider>
        <StaffListLayout
          header={<StaffListHeaderSection />}
          filters={<StaffListFiltersSection />}
          list={<StaffListTableSection />}
        />
      </StaffListProvider>
    </RequirePermission>
  )
}
