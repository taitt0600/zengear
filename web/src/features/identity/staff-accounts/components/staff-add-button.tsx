import { PlusIcon } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { newStaffPath } from '../utils/paths'

/**
 * Presentational: the link "Thêm nhân viên" to SCR-STF-02, in the header (default) and in the Empty of a list without
 * accounts (`outline`: the header holds the one primary action of the view, Layout of SCR-STF-01, #158).
 */
export function StaffAddButton({
  label,
  className,
  variant,
}: {
  label: string
  className?: string
  variant?: 'outline'
}) {
  return (
    <Button asChild className={className} variant={variant}>
      <Link href={newStaffPath}>
        <PlusIcon aria-hidden />
        {label}
      </Link>
    </Button>
  )
}
