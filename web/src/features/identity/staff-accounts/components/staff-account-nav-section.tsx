'use client'

import { useTranslations } from 'next-intl'
import { useEffect, useRef } from 'react'
import { useStaffAccountState } from '../hooks/staff-account-context'
import { useActiveSection } from '../hooks/use-active-section'
import { SectionNav, type SectionNavItem } from './section-nav'

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
  const ready = account !== undefined
  const { currentId, select } = useActiveSection(
    items.map((item) => item.id),
    ready,
  )
  useHashOnLoad(
    ready,
    items.map((item) => item.id),
  )

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

/** Scrolls once to the card that the address's hash names, when the account has loaded, as a browser's own hash. */
function useHashOnLoad(ready: boolean, ids: string[]) {
  const applied = useRef(false)
  const key = ids.join(' ')
  useEffect(() => {
    if (!ready || applied.current) return
    applied.current = true
    const id = window.location.hash.slice(1)
    if (key.split(' ').includes(id)) document.getElementById(id)?.scrollIntoView({ block: 'start' })
  }, [ready, key])
}
