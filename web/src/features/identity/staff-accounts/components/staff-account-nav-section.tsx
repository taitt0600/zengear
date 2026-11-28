'use client'

import { useTranslations } from 'next-intl'
import { SectionNav, type SectionNavItem } from '@/components/app/section-nav'
import { useActiveSection } from '@/hooks/use-active-section'
import { useHashOnLoad } from '@/hooks/use-hash-on-load'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { SECTIONS_END_ID } from '../utils/sections'

/**
 * The nav slot of SCR-STF-03 (its Layout, Section navigation): one link per card that shows, in page order, from the
 * first render, so that it shows while the cards are Skeleton; nothing on a 404 or a failed load. The cards of the HR
 * profile (STF-10) and the activity card (gap 10) add their ids when they are built. A hash of a card in the address is
 * applied once the account has loaded, not while the Skeleton shows.
 */
export function StaffAccountNavSection() {
  const t = useTranslations()
  const { account, error } = useStaffAccountState()
  const items: SectionNavItem[] = [
    { id: 'info', label: t('staff.account.info.title') },
    { id: 'account', label: t('staff.account.accountCard.title') },
  ]
  const ids = items.map((item) => item.id)
  const ready = account !== undefined
  const { currentId, select } = useActiveSection(ids, SECTIONS_END_ID, ready)
  useHashOnLoad(ready, ids)

  if (error) return null
  return (
    <SectionNav
      label={t('admin.sectionNav.label')}
      items={items}
      currentId={currentId}
      onSelect={select}
    />
  )
}
