'use client'

import { SearchXIcon, TriangleAlertIcon, UsersIcon } from 'lucide-react'
import { useFormatter, useTranslations } from 'next-intl'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useProblemText } from '@/hooks/use-problem-toast'
import { useStaffListState } from '../hooks/staff-list-context'
import { useCanCreateStaff } from '../hooks/use-can-create-staff'
import { apiDate, sortDirection, type SortField } from '../utils/staff-list'
import { StaffAddButton } from './staff-add-button'
import { StaffListEmpty } from './staff-list-empty'
import { StaffListPagination } from './staff-list-pagination'
import { STAFF_SKELETON_ROWS, StaffListRow } from './staff-list-row'
import { StaffSortHeader } from './staff-sort-header'

const DAY = { day: '2-digit', month: '2-digit', year: 'numeric' } as const

/**
 * The list of SCR-STF-01 (STF-05): the accounts of the page with their roles, status and dates, and its pages; or,
 * in place of the rows, skeleton rows while it loads and an Empty when there is nothing to show.
 */
export function StaffListTableSection() {
  const t = useTranslations()
  const format = useFormatter()
  const list = useStaffListState()
  const problemText = useProblemText()
  const canCreate = useCanCreateStaff()

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
  if (!list.loaded) return <StaffTable busy>{STAFF_SKELETON_ROWS}</StaffTable>
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
            <Button variant="outline" onClick={list.clearFilters}>
              {t('staff.list.clearFilters')}
            </Button>
          }
        />
        {list.totalCount > 0 && footer}
      </>
    )
  }
  const texts = {
    noValue: t('staff.list.noValue'),
    active: t('staff.list.status.active'),
    disabled: t('staff.list.status.disabled'),
  }
  const day = (value: string) => format.dateTime(apiDate(value), DAY)
  return (
    <>
      <StaffTable>
        {list.rows.map((row) => (
          <StaffListRow key={row.id} row={row} texts={texts} day={day} />
        ))}
      </StaffTable>
      {footer}
    </>
  )
}

// The table's box scrolls sideways itself (not shadcn's container inside it), so that the keyboard can scroll it
// once it has the focus; on a narrow screen it has a border.
function StaffTable({ busy, children }: { busy?: boolean; children: ReactNode }) {
  const t = useTranslations('staff.list')
  const { sort, toggleSort } = useStaffListState()
  const sorting = (field: SortField) => (
    <StaffSortHeader
      label={t(`columns.${field}`)}
      direction={sortDirection(sort, field)}
      onSort={() => toggleSort(field)}
    />
  )
  return (
    <div
      role="region"
      tabIndex={0}
      aria-label={t('tableLabel')}
      aria-busy={busy}
      className="overflow-x-auto rounded-lg border sm:border-0 [&>[data-slot=table-container]]:overflow-visible"
    >
      <Table>
        <TableHeader>
          <TableRow>
            {sorting('name')}
            {sorting('employeeCode')}
            <TableHead>{t('columns.email')}</TableHead>
            <TableHead>{t('columns.roles')}</TableHead>
            <TableHead>{t('columns.status')}</TableHead>
            {sorting('joinedDate')}
            <TableHead>{t('columns.leftDate')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{children}</TableBody>
      </Table>
    </div>
  )
}
