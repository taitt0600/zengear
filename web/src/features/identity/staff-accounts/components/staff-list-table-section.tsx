'use client'

import { SearchXIcon, TriangleAlertIcon, UsersIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useRef } from 'react'
import { Button } from '@/components/ui/button'
import { useCurrentUser } from '@/features/identity/access'
import { useProblemText } from '@/hooks/use-problem-toast'
import { useStaffListState } from '../hooks/staff-list-context'
import { useCanCreateStaff } from '../hooks/use-can-create-staff'
import { useCanDisableStaff } from '../hooks/use-can-disable-staff'
import { useDay, useStaffStatusTexts } from '../hooks/use-staff-texts'
import { StaffAddButton } from './staff-add-button'
import { StaffListEmpty } from './staff-list-empty'
import { StaffListPagination } from './staff-list-pagination'
import { STAFF_SKELETON_ROWS, StaffListRow } from './staff-list-row'
import { StaffTable } from './staff-list-table'

/**
 * The list of SCR-STF-01 (STF-05): the accounts of the page, numbered, with their roles, status, dates and menu of
 * actions, and its pages; or, in place of the rows, skeleton rows while it loads and an Empty when there is nothing to
 * show. Whether the user may lock or restore (`staff.disable`) and their id are read once, for every row.
 */
export function StaffListTableSection() {
  const t = useTranslations()
  const day = useDay()
  const statusTexts = useStaffStatusTexts()
  const list = useStaffListState()
  const problemText = useProblemText()
  const canCreate = useCanCreateStaff()
  const canDisable = useCanDisableStaff()
  const me = useCurrentUser()
  // Where the focus goes when a row leaves the list with its dialog open (StaffRowActions): the table's box (with its
  // rows or its skeleton rows), or "Xóa bộ lọc" when nothing matches the filters any more.
  const region = useRef<HTMLDivElement>(null)
  const clear = useRef<HTMLButtonElement>(null)
  const focusList = () => (region.current ?? clear.current)?.focus()

  if (list.error) {
    return (
      <StaffListEmpty
        tone="error"
        icon={<TriangleAlertIcon aria-hidden />}
        title={problemText(list.error)}
        action={
          <Button onClick={list.retry} disabled={list.isFetching}>
            {t('admin.retry')}
          </Button>
        }
      />
    )
  }
  if (!list.loaded) {
    return (
      <StaffTable ref={region} busy>
        {STAFF_SKELETON_ROWS}
      </StaffTable>
    )
  }
  if (list.totalCount === 0 && !list.filtered) {
    return (
      <StaffListEmpty
        icon={<UsersIcon aria-hidden />}
        title={t('staff.list.empty.title')}
        description={t('staff.list.empty.description')}
        action={canCreate ? <StaffAddButton label={t('staff.list.add')} /> : undefined}
      />
    )
  }
  const footer = (
    <StaffListPagination
      total={t('staff.list.total', { count: list.totalCount })}
      position={t('staff.list.page', { page: list.page, pages: list.pages })}
      previous={list.page > 1 ? list.pageHref(list.page - 1) : undefined}
      next={list.page < list.pages ? list.pageHref(list.page + 1) : undefined}
    />
  )
  if (list.rows.length === 0) {
    // In place of the rows only: when the search hid them, the pages stay, to look on another one.
    return (
      <>
        <StaffListEmpty
          icon={<SearchXIcon aria-hidden />}
          title={t('staff.list.noMatch.title')}
          description={t('staff.list.noMatch.description')}
          action={
            <Button ref={clear} variant="outline" onClick={list.clearFilters}>
              {t('staff.list.clearFilters')}
            </Button>
          }
        />
        {list.totalCount > 0 && footer}
      </>
    )
  }
  const texts = { noValue: t('staff.list.noValue'), status: statusTexts }
  return (
    <>
      <StaffTable ref={region}>
        {list.rows.map((row) => (
          <StaffListRow
            key={row.id}
            row={row}
            texts={texts}
            day={day}
            canDisable={canDisable}
            meId={me.id}
            onGone={focusList}
          />
        ))}
      </StaffTable>
      {footer}
    </>
  )
}
