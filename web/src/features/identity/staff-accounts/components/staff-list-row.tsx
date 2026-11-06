import Link from 'next/link'
import { Skeleton } from '@/components/ui/skeleton'
import { TableCell, TableRow } from '@/components/ui/table'
import type { StaffListItemDto } from '@/lib/api/generated/model'
import { staffPath } from '../utils/paths'
import { StaffRolesCell } from './staff-roles-cell'
import { StaffRowActions } from './staff-row-actions'
import { StaffStatusBadge, type StaffStatusTexts } from './staff-status-badge'

/** The texts of a row: the mark of an empty value and the label of each status. */
export type StaffRowTexts = { noValue: string; status: StaffStatusTexts }

/**
 * Presentational: one account of SCR-STF-01, its "STT" first, its name a link to SCR-STF-03, its menu "Thao tác" last;
 * an empty code or joined date shows `noValue`, the left date stays empty while the person is employed. `day` shows a
 * date of the API. The menu offers the commands when the user holds `staff.disable` (`canDisable`) and the row is not
 * their own account (`meId`, R-USR-12); `onGone` takes the focus when the row leaves the list with a dialog open.
 */
export function StaffListRow({
  row,
  texts,
  day,
  canDisable,
  meId,
  onGone,
}: {
  row: StaffListItemDto & { number: number }
  texts: StaffRowTexts
  day: (value: string) => string
  canDisable: boolean
  meId: string
  onGone: () => void
}) {
  return (
    <TableRow>
      <TableCell className="text-right tabular-nums">{row.number}</TableCell>
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
        <StaffStatusBadge status={row.status} texts={texts.status} />
      </TableCell>
      <TableCell className="tabular-nums">
        {row.joinedDate ? day(row.joinedDate) : texts.noValue}
      </TableCell>
      <TableCell className="tabular-nums">{row.leftDate && day(row.leftDate)}</TableCell>
      <TableCell>
        <StaffRowActions row={row} canAct={canDisable && row.id !== meId} onGone={onGone} />
      </TableCell>
    </TableRow>
  )
}

const SKELETON_WIDTHS = ['w-6', 'w-36', 'w-16', 'w-48', 'w-24', 'w-28', 'w-20', 'w-20', 'w-8']

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
