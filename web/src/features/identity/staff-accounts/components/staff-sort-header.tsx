import { ArrowDownIcon, ArrowUpDownIcon, ArrowUpIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { TableHead } from '@/components/ui/table'

const ICONS = { ascending: ArrowUpIcon, descending: ArrowDownIcon, none: ArrowUpDownIcon }

/**
 * Presentational: the header cell of a column that sorts (SCR-STF-01), whose button sorts by it; `aria-sort` only on
 * the column the list is sorted by.
 */
export function StaffSortHeader({
  label,
  direction,
  onSort,
}: {
  label: string
  direction: 'ascending' | 'descending' | 'none'
  onSort: () => void
}) {
  const Icon = ICONS[direction]
  return (
    <TableHead aria-sort={direction === 'none' ? undefined : direction}>
      <Button variant="ghost" size="sm" className="-ml-2.5" onClick={onSort}>
        {label}
        <Icon aria-hidden className={cn(direction === 'none' && 'text-muted-foreground')} />
      </Button>
    </TableHead>
  )
}
