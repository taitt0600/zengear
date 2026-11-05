import type { Metadata } from 'next'
import { getTranslations } from 'next-intl/server'
import {
  StaffAccountDetailsSection,
  StaffAccountHeadingSection,
  StaffAccountLayout,
  StaffAccountProvider,
  StaffAccountStatusSection,
} from '@/features/identity/staff-accounts'
import { RequirePermission } from '@/features/shell/admin'

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('staff.account')
  return { title: t('title') }
}

/**
 * SCR-STF-03 Staff account (STF-01, STF-02): one account, with the way to lock or restore it, for a user who holds
 * staff.read. The id comes from the URL, not from the API, so it is encoded before it goes into the API's path
 * (docs/FRONTEND.md, Data from the API); the API answers 404 for an id it does not know.
 */
export default async function StaffAccountPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  return (
    <RequirePermission permission="staff.read">
      <StaffAccountProvider id={encodeURIComponent(id)}>
        <StaffAccountLayout
          heading={<StaffAccountHeadingSection />}
          details={<StaffAccountDetailsSection />}
          status={<StaffAccountStatusSection />}
        />
      </StaffAccountProvider>
    </RequirePermission>
  )
}
