'use client'

import { CalendarIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useDay } from '../hooks/use-staff-texts'
import { calendarDate, calendarDay } from '../utils/staff-account'

/**
 * The field "Ngày nghỉ việc" of the dialog "Khóa tài khoản" (SCR-STF-03), split from it for its size: a date picker of
 * one month, Monday first, today in Vietnam marked, whose days after it and before the joined date are disabled (R-USR-07, the
 * API still checks); `required`, so that a second click never empties it. `error` is the API's text of a 400 or 409
 * on `#/leftDate`.
 */
export function StaffLeftDateField({
  value,
  onChange,
  today,
  joinedDate,
  error,
}: {
  value: string
  onChange: (value: string) => void
  today: string
  joinedDate: string | null
  error?: string
}) {
  const t = useTranslations()
  const show = useDay()
  const [open, setOpen] = useState(false)
  const id = useId()
  const button = useRef<HTMLButtonElement>(null)
  // A refusal of the date takes the focus to the field, as an error of the other forms does.
  useEffect(() => {
    if (error) button.current?.focus()
  }, [error])
  const selected = calendarDate(value)
  const latest = calendarDate(today)
  const earliest = joinedDate ? calendarDate(joinedDate) : undefined
  const described = [`${id}-hint`, error && `${id}-error`].filter(Boolean).join(' ')

  return (
    <Field data-invalid={error ? true : undefined}>
      <FieldLabel id={`${id}-label`} htmlFor={id}>
        {t('staff.list.columns.leftDate')}
      </FieldLabel>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          {/* Named by its label and the day it shows. */}
          <Button
            ref={button}
            id={id}
            type="button"
            variant="outline"
            className="w-60 justify-between font-normal"
            aria-labelledby={`${id}-label ${id}`}
            aria-describedby={described}
            aria-invalid={error ? true : undefined}
          >
            {show(value)}
            <CalendarIcon data-icon="inline-end" aria-hidden />
          </Button>
        </PopoverTrigger>
        {/* The chosen day takes the focus, not the first month button. Not the Calendar's `autoFocus`: it focuses
            before this popover's focus scope pauses the dialog's, which takes the focus back. */}
        <PopoverContent
          className="w-auto p-0"
          align="start"
          onOpenAutoFocus={(event) => {
            event.preventDefault()
            // The only day react-day-picker leaves in the tab order: the chosen one.
            const content = event.currentTarget as HTMLElement
            content.querySelector<HTMLElement>('[role=grid] button[tabindex="0"]')?.focus()
          }}
        >
          <Calendar
            mode="single"
            required
            selected={selected}
            defaultMonth={selected}
            weekStartsOn={1}
            today={latest}
            startMonth={earliest}
            endMonth={latest}
            disabled={earliest ? [{ after: latest }, { before: earliest }] : { after: latest }}
            onSelect={(date) => {
              onChange(calendarDay(date))
              setOpen(false)
            }}
          />
        </PopoverContent>
      </Popover>
      <FieldDescription id={`${id}-hint`}>
        {joinedDate
          ? t('staff.account.disableDialog.leftDateHint', { joinedDate: show(joinedDate) })
          : t('staff.account.disableDialog.leftDateHintToday')}
      </FieldDescription>
      {error && <FieldError id={`${id}-error`}>{error}</FieldError>}
    </Field>
  )
}
