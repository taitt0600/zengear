import Link from 'next/link'
import { Skeleton } from '@/components/ui/skeleton'
import { TableCell, TableRow } from '@/components/ui/table'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import { staffPath } from '../utils/paths'
import { StaffRolesCell } from './staff-roles-cell'
import { StaffStatusBadge } from './staff-status-badge'

/** The texts of a row: the mark of an empty value and the label of each status. */
export type StaffRowTexts = { noValue: string; active: string; disabled: string }

/**
 * Presentational: one account of SCR-STF-01, its name a link to SCR-STF-03; an empty code or joined date shows
 * `noValue`, the left date stays empty while the person is employed. `day` shows a date of the API.
 */
export function StaffListRow({
  row,
  texts,
  day,
}: {
  row: StaffListItemDto
  texts: StaffRowTexts
  day: (value: string) => string
}) {
  return (
    <TableRow>
      <TableCell>
        <Link href={staffPath(row.id)} className="font-medium underline underline-offset-4">
          {row.name}
        </Link>
      </TableCell>
      <TableCell>{row.employeeCode ?? texts.noValue}</TableCell>
      <TableCell>{row.email}</TableCell>
      <TableCell>
        <StaffRolesCell roles={row.roles} />
      </TableCell>
      <TableCell>
        <StaffStatusBadge
          status={row.status}
          label={row.status === 'ACTIVE' ? texts.active : texts.disabled}
        />
      </TableCell>
      <TableCell className="tabular-nums">
        {row.joinedDate ? day(row.joinedDate) : texts.noValue}
      </TableCell>
      <TableCell className="tabular-nums">{row.leftDate && day(row.leftDate)}</TableCell>
    </TableRow>
  )
}

const SKELETON_WIDTHS = ['w-36', 'w-16', 'w-48', 'w-24', 'w-28', 'w-20', 'w-20']

/** Presentational: the three rows of Skeleton, one bar a column, that stand for the rows while the list loads. */
export const STAFF_SKELETON_ROWS = [0, 1, 2].map((row) => (
  <TableRow key={row}>
    {SKELETON_WIDTHS.map((width, cell) => (
      <TableCell key={cell}>
        <Skeleton className={`h-4 ${width}`} />
      </TableCell>
    ))}
  </TableRow>
))
