'use client'

import { useTranslations } from 'next-intl'
import type { ReactNode, Ref } from 'react'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useStaffListState } from '../hooks/staff-list-context'
import { sortDirection, type SortField } from '../utils/staff-list'
import { StaffSortHeader } from './staff-sort-header'

/**
 * The table of SCR-STF-01 with its nine column headers, "STT" first (`aria-hidden`, read as "Số thứ tự") and "Thao tác"
 * last; the sorting headers change the URL's sort. Its box scrolls sideways itself (not shadcn's container inside it),
 * so that the keyboard can scroll it once it has the focus; on a narrow screen it has a border.
 */
export function StaffTable({
  busy,
  children,
  ref,
}: {
  busy?: boolean
  children: ReactNode
  ref?: Ref<HTMLDivElement>
}) {
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
      ref={ref}
      role="region"
      tabIndex={0}
      aria-label={t('tableLabel')}
      aria-busy={busy}
      className="overflow-x-auto rounded-lg border sm:border-0 [&>[data-slot=table-container]]:overflow-visible"
    >
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="text-right">
              <span aria-hidden>{t('columns.number')}</span>
              <span className="sr-only">{t('columns.numberFull')}</span>
            </TableHead>
            {sorting('name')}
            {sorting('employeeCode')}
            <TableHead>{t('columns.email')}</TableHead>
            <TableHead>{t('columns.roles')}</TableHead>
            <TableHead>{t('columns.status')}</TableHead>
            {sorting('joinedDate')}
            <TableHead>{t('columns.leftDate')}</TableHead>
            <TableHead>{t('columns.actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{children}</TableBody>
      </Table>
    </div>
  )
}
