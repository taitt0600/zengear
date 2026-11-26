'use client'

import { SearchIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { useStaffListState } from '../hooks/staff-list-context'
import { StaffStatusNav } from './staff-status-nav'

/**
 * The filters of SCR-STF-01: the search over the rows of the page, which stays in the browser (DEC-105), and the
 * status links with the count waiting (STF-09), kept in the URL. The role filter waits for an endpoint that lists the roles (docs/screens/gaps.md, gap 2).
 */
export function StaffListFiltersSection() {
  const t = useTranslations('staff.list')
  const list = useStaffListState()
  const searchId = useId()
  const hintId = useId()
  const hint = list.pages > 1
  return (
    // The status links on their own row, the search under them (SCR-STF-01, Layout of 2026-11-13).
    <div className="flex flex-col gap-4">
      <StaffStatusNav list={list} />
      <Field className="sm:max-w-[420px]">
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
    </div>
  )
}
