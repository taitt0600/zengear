import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { Controller } from 'react-hook-form'
import { FormProblems } from '@/components/app/form-problems'
import { RequiredMark } from '@/components/app/required-note'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { AccountForm } from '../hooks/use-account-form'
import { SubmitButton } from './submit-button'

/**
 * The name form of SCR-ACC-01 (its Layout, Profile card): the display name, required, with "Lưu" beside it on the same
 * line (the input grows, the button keeps its width) and its hint under both.
 */
export function AccountProfileFields({
  control,
  problems,
  sending,
  canSave,
  onSubmit,
}: AccountForm) {
  const t = useTranslations('account')
  const id = useId()
  return (
    <form noValidate onSubmit={onSubmit}>
      <FieldGroup>
        <FormProblems problems={problems} />
        <Controller
          name="name"
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={`${id}-name`}>
                {t('name')}
                <RequiredMark />
              </FieldLabel>
              <div className="flex gap-2">
                <Input
                  {...field}
                  id={`${id}-name`}
                  autoComplete="name"
                  required
                  aria-describedby={`${id}-name-hint`}
                  aria-invalid={fieldState.invalid}
                  className="min-w-0 flex-1"
                />
                <SubmitButton sending={sending} disabled={sending || !canSave}>
                  {t('save')}
                </SubmitButton>
              </div>
              <FieldDescription id={`${id}-name-hint`}>{t('nameHint')}</FieldDescription>
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
      </FieldGroup>
    </form>
  )
}

/** The name form while GET /me loads: in place, never an empty card. */
export function AccountProfileSkeleton() {
  return (
    <div className="flex flex-col gap-3">
      <Skeleton className="h-4 w-24" />
      <div className="flex gap-2">
        <Skeleton className="h-9 flex-1" />
        <Skeleton className="h-9 w-16" />
      </div>
      <Skeleton className="h-4 w-32" />
    </div>
  )
}
