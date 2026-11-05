'use client'

import { useTranslations } from 'next-intl'
import { CardAction, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useCanCreateStaff } from '../hooks/use-can-create-staff'
import { StaffAddButton } from './staff-add-button'

/**
 * The header of SCR-STF-01: the page's h1 and what the list is for, with "Thêm nhân viên" for a user who holds
 * `staff.create`. On a narrow screen the button goes under the text, full width (shadcn's CardHeader keeps its two
 * columns at every width, so it stacks only below `sm` here).
 */
export function StaffListHeaderSection() {
  const t = useTranslations()
  const canCreate = useCanCreateStaff()
  return (
    <CardHeader className="flex flex-col sm:grid">
      <CardTitle>
        <h1 className="text-xl">{t('admin.menu.staff')}</h1>
      </CardTitle>
      <CardDescription>{t('staff.list.description')}</CardDescription>
      {canCreate && (
        <CardAction className="self-stretch sm:self-start">
          <StaffAddButton label={t('staff.list.add')} className="w-full sm:w-auto" />
        </CardAction>
      )}
    </CardHeader>
  )
}
