import { PlusIcon } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { newStaffPath } from '../utils/paths'

/** Presentational: the link "Thêm nhân viên" to SCR-STF-02, in the header and in the Empty of a list without accounts. */
export function StaffAddButton({ label, className }: { label: string; className?: string }) {
  return (
    <Button asChild className={className}>
      <Link href={newStaffPath}>
        <PlusIcon aria-hidden />
        {label}
      </Link>
    </Button>
  )
}
