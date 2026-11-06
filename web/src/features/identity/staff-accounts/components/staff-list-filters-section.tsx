'use client'

import { SearchIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useStaffListState } from '../hooks/staff-list-context'
import type { StatusFilter } from '../hooks/use-staff-list'

const STATUSES = { all: 'all', ACTIVE: 'active', DISABLED: 'disabled' } as const satisfies Record<
  StatusFilter,
  string
>

/**
 * The filters of SCR-STF-01: the search over the rows of the page, which stays in the browser (DEC-105), and the
 * status, kept in the URL. The role filter waits for an endpoint that lists the roles (docs/SCREENS.md, gap 2).
 */
export function StaffListFiltersSection() {
  const t = useTranslations('staff.list')
  const list = useStaffListState()
  const searchId = useId()
  const hintId = useId()
  const statusId = useId()
  const hint = list.pages > 1
  return (
    // Aligned at the top: the search hint, under the search box only, adds a line that must not push the status down.
    <div className="flex flex-col gap-4 sm:flex-row sm:flex-wrap sm:items-start">
      <Field className="sm:max-w-[420px] sm:flex-1">
        <FieldLabel htmlFor={searchId}>{t('search')}</FieldLabel>
        <InputGroup>
          <InputGroupInput
            id={searchId}
            type="search"
            name="search"
            autoComplete="off"
            spellCheck={false}
            placeholder={t('searchPlaceholder')}
            value={list.search}
            onChange={(event) => list.setSearch(event.target.value)}
            aria-describedby={hint ? hintId : undefined}
          />
          <InputGroupAddon>
            <SearchIcon aria-hidden />
          </InputGroupAddon>
        </InputGroup>
        {hint && <FieldDescription id={hintId}>{t('searchHint')}</FieldDescription>}
      </Field>
      <Field className="sm:w-[200px]">
        <FieldLabel htmlFor={statusId}>{t('columns.status')}</FieldLabel>
        <Select
          value={list.status}
          onValueChange={(value) => list.setStatus(value as StatusFilter)}
        >
          <SelectTrigger id={statusId} className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectGroup>
              {Object.entries(STATUSES).map(([value, key]) => (
                <SelectItem key={value} value={value}>
                  {t(`status.${key}`)}
                </SelectItem>
              ))}
            </SelectGroup>
          </SelectContent>
        </Select>
      </Field>
    </div>
  )
}
