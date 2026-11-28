import type { ReactNode } from 'react'
import type {
  ControllerFieldState,
  ControllerRenderProps,
  FieldValues,
  Path,
} from 'react-hook-form'
import { RequiredMark } from '@/components/app/required-note'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'

/**
 * A password field of a form of this module, drawn from the render props of a react-hook-form Controller. `required`
 * marks it (docs/screens/conventions.md, Required fields); `describedBy` names what describes it when that is not the
 * `description` (the items of a checklist, given as `children` under the input).
 */
export function PasswordField<T extends FieldValues, N extends Path<T>>({
  id,
  label,
  autoComplete,
  description,
  describedBy,
  required = false,
  field,
  fieldState,
  children,
}: {
  id: string
  label: string
  autoComplete: 'new-password' | 'current-password'
  description?: string
  describedBy?: string
  required?: boolean
  field: ControllerRenderProps<T, N>
  fieldState: ControllerFieldState
  children?: ReactNode
}) {
  return (
    <Field data-invalid={fieldState.invalid}>
      <FieldLabel htmlFor={id}>
        {label}
        {required && <RequiredMark />}
      </FieldLabel>
      <Input
        {...field}
        id={id}
        type="password"
        autoComplete={autoComplete}
        required={required}
        aria-describedby={describedBy ?? (description ? `${id}-help` : undefined)}
        aria-invalid={fieldState.invalid}
      />
      {description && <FieldDescription id={`${id}-help`}>{description}</FieldDescription>}
      {children}
      {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
    </Field>
  )
}
