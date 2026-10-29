import { useTranslations } from 'next-intl'
import { useId } from 'react'
import { Controller } from 'react-hook-form'
import { FormProblems } from '@/components/app/form-problems'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { AccountForm } from '../hooks/use-account-form'
import { SubmitButton } from './submit-button'

/** The fields of SCR-ACC-01: the display name, the email read-only with its note, and "Lưu". */
export function AccountProfileFields({
  control,
  problems,
  sending,
  canSave,
  onSubmit,
  email,
}: AccountForm & { email: string | null | undefined }) {
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
              <FieldLabel htmlFor={`${id}-name`}>{t('name')}</FieldLabel>
              <Input
                {...field}
                id={`${id}-name`}
                autoComplete="name"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
            </Field>
          )}
        />
        <Field>
          <FieldLabel htmlFor={`${id}-email`}>{t('email')}</FieldLabel>
          <Input
            id={`${id}-email`}
            type="email"
            value={email ?? ''}
            readOnly
            aria-describedby={`${id}-email-note`}
          />
          <FieldDescription id={`${id}-email-note`}>{t('emailNote')}</FieldDescription>
        </Field>
        <div>
          <SubmitButton sending={sending} disabled={sending || !canSave}>
            {t('save')}
          </SubmitButton>
        </div>
      </FieldGroup>
    </form>
  )
}

/** The fields while GET /me loads: in place, never an empty screen. */
export function AccountProfileSkeleton() {
  return (
    <FieldGroup>
      {[0, 1].map((field) => (
        <Field key={field}>
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-9 w-full" />
        </Field>
      ))}
      <Skeleton className="h-9 w-16" />
    </FieldGroup>
  )
}
