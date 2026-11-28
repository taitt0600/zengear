'use client'

import { useQuery } from '@tanstack/react-query'
import { useTranslations } from 'next-intl'
import { SectionNav, type SectionNavItem } from '@/components/app/section-nav'
import { useActiveSection } from '@/hooks/use-active-section'
import { useHashOnLoad } from '@/hooks/use-hash-on-load'
import { meQuery } from '@/features/identity/access'
import { SECTIONS_END_ID } from '../utils/sections'

/**
 * The nav slot of SCR-ACC-01 (its Layout, Width and section navigation, as SCR-STF-03): one link per card, in page
 * order, shown while GET /me loads too, since the cards keep their titles then. A hash of a card in the address is
 * applied once GET /me has answered, when the cards have their heights.
 */
export function AccountNavSection() {
  const t = useTranslations()
  const ready = useQuery(meQuery).data !== undefined
  const items: SectionNavItem[] = [
    { id: 'profile', label: t('account.profile.title') },
    { id: 'password', label: t('account.passwordCard.title') },
  ]
  const ids = items.map((item) => item.id)
  const { currentId, select } = useActiveSection(ids, SECTIONS_END_ID, ready)
  useHashOnLoad(ready, ids)

  return (
    <SectionNav
      label={t('admin.sectionNav.label')}
      items={items}
      currentId={currentId}
      onSelect={select}
    />
  )
}
